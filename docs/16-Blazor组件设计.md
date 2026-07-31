# Blazor 组件设计

## 1. 设计目标

- 组件围绕用户操作和业务投影拆分，避免一个 Dashboard 承担全部状态。
- 领域模型不直接暴露给 Razor，页面使用 Application Query DTO/ViewModel。
- 支持 Blazor Web App 的静态 SSR + Interactive Server 渐进交互。
- 加载、错误、取消和降级状态具有统一组件契约。

## 2. 渲染模式

v1.0 默认：

- 应用外壳和非交互内容使用静态 SSR。
- Dashboard、地图、图表、收藏和设置使用 Interactive Server。
- 交互连接中断时保留已渲染结果，并提供重连/刷新状态。
- 对外 HTTP API 与组件用例复用 Application 层，不在组件中直接调用 Open-Meteo、Stormglass 或 WorldTides。

若后续 PWA 需要更强离线能力，再评估 Interactive WebAssembly/Auto，不在 MVP 同时维护两套复杂状态。

## 3. 目录结构

```text
Components/
├── Layout/
│   ├── MainLayout.razor
│   ├── AppNavigation.razor
│   └── ConnectionStatus.razor
├── Pages/
│   ├── Dashboard.razor
│   ├── MapPicker.razor
│   ├── Favorites.razor
│   ├── QueryHistory.razor
│   ├── Settings.razor
│   └── Admin/
├── Features/
│   ├── ForecastQuery/
│   ├── MarineAnalysis/
│   ├── ForecastCharts/
│   ├── LocationPicker/
│   ├── Favorites/
│   └── UserSettings/
└── Shared/
    ├── AsyncStateView.razor
    ├── RiskBadge.razor
    ├── MetricValue.razor
    ├── DataSourceStatusList.razor
    └── EmptyState.razor
```

每个复杂组件使用同名 `.razor.cs` 和 `.razor.css`；简单展示组件可保持单文件。

## 4. 组件分层

| 层级 | 职责 | 示例 |
| --- | --- | --- |
| 页面组件 | 路由、查询参数、页面级编排 | `Dashboard`, `Favorites` |
| Feature 容器 | 获取数据、管理局部状态、组合业务组件 | `MarineAnalysisPanel` |
| 业务展示组件 | 展示明确业务概念，无数据访问 | `RiskSummary`, `ActivityScoreList` |
| 基础组件 | 通用状态、格式和交互 | `AsyncStateView`, `MetricValue` |
| JS 互操作适配 | 地图、图表等第三方生命周期 | `LeafletMap`, `ForecastChart` |

## 5. 核心组件清单

| 组件 | 关键参数/事件 | 职责 |
| --- | --- | --- |
| `ForecastQueryBar` | `Query`, `OnSubmit`, `IsBusy` | 地点、时间、范围和活动输入 |
| `LocationSearchBox` | `Value`, `OnSelected`, `SearchAsync` | 防抖搜索和候选键盘导航 |
| `ActivitySelector` | `Selected`, `OnChanged` | 活动分段切换 |
| `RiskSummary` | `Overall`, `TopRisks`, `SourceQuality` | 综合结论和硬性警示 |
| `ActivityScoreList` | `Items`, `Selected`, `OnSelected` | 活动评分切换 |
| `MetricGrid` | `ForecastPoint`, `RiskContributions` | 稳定网格展示关键指标 |
| `ForecastTrendTabs` | `Hourly`, `SelectedMetric` | 风、浪、分数趋势 |
| `RecommendationTimeline` | `Windows`, `RiskTurningPoints` | 推荐窗口和返航截止 |
| `HourlyDetailPanel` | `Assessment`, `IsOpen`, `OnClose` | 完整指标和规则贡献 |
| `DataSourceStatusList` | `Sources` | 分数据域展示 Provider、模型、时效、缓存与质量 |
| `LeafletMapPicker` | `Center`, `SelectedPoint`, `OnPointChanged` | 地图选点 |

## 6. 组件接口约定

- 参数使用不可变 DTO 或只读集合，禁止组件修改父级传入对象。
- 状态变化使用 `EventCallback<T>`，事件名使用 `OnXxx`。
- 可取消异步事件传入页面级 `CancellationToken` 或由容器管理 `CancellationTokenSource`。
- 展示组件不注入 Repository、DbContext 或 Provider。
- 需要错误边界的第三方组件外包裹 `ErrorBoundary`，但业务错误由显式状态展示。

示例：

```csharp
public sealed partial class RiskSummary
{
    [Parameter, EditorRequired]
    public required OverallAssessmentViewModel Overall { get; set; }

    [Parameter]
    public IReadOnlyList<RiskFactorViewModel> TopRisks { get; set; } = [];
}
```

## 7. 状态管理

| 状态 | 位置 | 生命周期 |
| --- | --- | --- |
| 查询表单 | `DashboardState`（Scoped） | 当前交互会话 |
| 当前分析结果 | Feature 容器/Scoped Store | 切页可保留，刷新可恢复查询参数 |
| 用户设置 | `UserPreferenceState` | 登录会话 + 服务端持久化 |
| 收藏列表 | 页面 Query + 小型缓存 | 按需刷新 |
| 图表 Hover/Tab | 组件本地状态 | 组件生命周期 |
| Provider/算法管理 | 管理页面本地状态 | 页面生命周期 |

不引入全局状态框架作为 MVP 前置条件。跨组件状态先使用 Scoped State Container 和不可变快照；复杂度真实增加后再评估 Fluxor。

当前 `MI-0015` 使用 scoped `DashboardQuerySession` 管理地点搜索、候选选择、请求取消和查询版本；`MI-0017` 已让该状态容器投影综合结论、五类活动评分、主要风险和逐小时评分表；`MI-0018` 已继续投影推荐时间窗、风险上升点和返航截止。页面组件只负责表单绑定和状态展示。

## 8. 数据加载与取消

- `OnInitializedAsync` 只加载页面必需数据；图表和历史按可见性延迟加载。
- 新查询开始时取消上一请求，防止晚返回结果覆盖新条件。
- 使用单调递增请求版本或 QueryId，应用结果前检查是否仍为当前请求。
- 首次加载显示同尺寸骨架；刷新保留旧结果并显示局部忙状态。
- 组件释放时取消请求并释放 JS 对象引用。

## 9. 表单与验证

- 使用 `EditForm` 和独立 Query Input Model，不直接绑定领域实体。
- 经纬度、时间范围、活动类型在客户端提供即时反馈，服务端再次验证。
- 搜索候选遵循组合框键盘交互和 ARIA 约定。
- 提交期间禁用重复提交，但保留取消按钮。
- 服务端 `ProblemDetails.errors` 映射到字段或页面级错误区域。

## 10. JS 互操作

- Leaflet 和 ApexCharts 通过小型 ES Module 封装，不在多个组件复制全局 JS。
- 组件初始化后创建实例，参数变化执行增量更新，Dispose 时销毁。
- JS 回调进入 .NET 前验证数据范围，避免把第三方对象直接传入领域逻辑。
- 地图和图表加载失败时提供列表/输入替代能力，页面仍可查询。

## 11. 性能规范

- 使用 `@key` 保持逐小时项和风险项身份稳定。
- 对 168 小时列表使用分页、虚拟化或图表聚合，不一次渲染大量复杂 DOM。
- 避免在 Razor 渲染期间重复排序、单位换算和创建集合，提前生成 ViewModel。
- 只在参数真实变化时刷新第三方图表。
- 指标网格和工具栏设置稳定尺寸，加载文本不得改变整体布局。
- 通过浏览器性能工具检查重渲染、SignalR 负载和首屏交互时间。

## 12. 测试

- bUnit：参数渲染、风险状态、缺失数据、事件回调和授权视图。
- 单元测试：State Container 的取消、请求版本和错误状态。
- Playwright：地点查询、活动切换、小时详情、收藏和移动端布局。
- 视觉回归：360x800、768x1024、1440x900，检查文字截断、重叠和图表非空。
- JS 互操作测试：地图选点、图表更新、Dispose 和失败降级。

当前 `MI-0015` 自动化覆盖根 Dashboard SSR 壳层、`DashboardQuerySession` 地点搜索、成功查询投影和 Provider 失败错误状态；`MI-0017` 追加活动评分、综合结论和主要风险投影测试；`MI-0018` 追加推荐时间窗投影测试。按用户指令，本次视觉效果由用户自行人工验证。

## 13. 变更记录

| 版本 | 日期 | 变更说明 |
| --- | --- | --- |
| 1.0 | 2026-07-13 | 定义 Blazor 渲染模式、组件边界、状态和测试策略 |
| 1.1 | 2026-07-13 | 更新为多 Provider 数据源边界 |
| 1.2 | 2026-07-30 | 记录 `MI-0015` Dashboard scoped 状态容器、metrics-only 结果投影和测试覆盖 |
| 1.3 | 2026-07-30 | 记录 `MI-0017` DashboardQuerySession 活动评分、综合结论和风险摘要投影 |
| 1.4 | 2026-07-31 | 记录 `MI-0018` DashboardQuerySession 推荐窗口、风险上升和返航截止投影 |
