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
│   ├── UserLocations.razor
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
| `AdminTabs` | 无（静态 NavLink） | 后台管理 Tab 导航（运行状态 / 预置地点 / 用户），`[Authorize(Policy="Administrator")]` |
| `AdminLocations` | `AdminLocationService` | 预置地点表格 + 新增/编辑表单 + 天地图选点 + 删除确认（级联收藏引用数提示） |
| `AdminUsers` | `AdminUserService` | 已注册用户只读列表 |

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

当前 `MI-0015` 使用 scoped `DashboardQuerySession` 管理地点搜索、候选选择、请求取消和查询版本；`MI-0017` 已让该状态容器投影综合结论、五类活动评分、主要风险和逐小时评分表；`MI-0018` 已继续投影推荐时间窗、风险上升点和返航截止；`MI-0019` 已增加趋势 Tabs、时间带、选中小时和小时详情 ViewModel；`MI-0024` 已增加地图/坐标目标状态，预置地点和自定义坐标都通过同一分析查询入口提交。页面组件只负责表单绑定、地图 JS 回调、Tab/小时选择和状态展示。

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

- Leaflet 和 ECharts 通过小型 ES Module 封装，不在多个组件复制全局 JS。
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

当前 `MI-0015` 自动化覆盖根 Dashboard SSR 壳层、`DashboardQuerySession` 地点搜索、成功查询投影和 Provider 失败错误状态；`MI-0017` 追加活动评分、综合结论和主要风险投影测试；`MI-0018` 追加推荐时间窗投影测试；`MI-0019` 追加趋势 Tabs、时间带、默认小时详情、切换趋势和选中小时测试；`MI-0024` 追加地图/坐标 SSR 壳层、自定义坐标提交和非法坐标降级测试。按用户指令，本次视觉效果由用户自行人工验证。

`MI-0026` 的账户页面通过 `[ExcludeFromInteractiveRouting]` 强制静态 SSR，使 Identity Cookie 可在普通 HTTP 响应中写入或清除；应用路由使用 `AuthorizeRouteView`，Header 的 `AccountNav` 通过 `AuthorizeView` 投影匿名和登录状态。注册、登录和退出均提交带防伪令牌的普通表单，成功后整页跳转，Dashboard 的 Interactive Server 查询状态不承担认证 Cookie 写入。

`MI-0027` 的 `DashboardQuerySession` 在服务端投影阶段完成单位换算，并保存当前请求活动供收藏与历史复用。Dashboard 将 `locationId`、`hours`、`from` 和 `activity` 查询参数恢复到同一状态容器；`from` 以字符串接收并按 Round-trip UTC 显式解析，避免 Blazor 不支持可空 `DateTimeOffset` 查询参数导致 SSR 500。收藏、历史、设置和管理员页均使用授权路由及 Interactive Server 交互。

`MI-0041` 将品牌与 SEO 职责拆在既有组件边界内：`App.razor` 提供 SSR 可见的 Description、Open Graph/Twitter 元数据及 favicon/manifest；各路由使用唯一 `PageTitle`，根 `Dashboard.razor` 再通过 `HeadContent` 固化首页 canonical；`MainLayout.razor` 仅负责可见品牌组合和导航。静态 `robots.txt`、`sitemap.xml` 与品牌 PNG 位于 `wwwroot`，不引入运行时图片处理或额外 JS。

`MI-0043` 保持既有业务组件和状态容器边界不变，只重构呈现层：`MainLayout.razor` 增加移动底部主导航，`app.css` 提供跨页面深海色板、Header、账户与用户工作区基础样式，`Dashboard.razor.css` 独立维护查询工作台、摘要、活动/风险、趋势、来源、指标和逐小时表格。地图选点、收藏、AI 解读、时区和查询事件仍由原组件与 `DashboardQuerySession` 处理，避免视觉改造引入重复状态；`About`、`UserLocations`、`AdminLocations` 和 `AdminTabs` 的 scoped CSS 仅覆盖自身视觉。E2E 同时验证地图辅助入口默认收起、1440×900 与 360×800 无横向溢出以及账户页响应式。

`MI-0045` 不新增日期时间状态或第三方组件：`Dashboard.razor` 继续以 `datetime-local` 和 `OnForecastStartChanged` 维护同一查询语义，仅增加 `.datetime-control` 展示容器、时区徽标和无交互 SVG 图标。Chromium 的原生 calendar indicator 透明扩展到右侧选择热区，SVG 使用 `pointer-events:none`，所以点击仍由原生输入处理；不需要 JS 互操作，也不改变时区转换或表单提交逻辑。

`MI-0047` 继续复用同一日期时间状态：移动端增加由 `ForecastStartLocal` 派生的 `.datetime-display`，以 `yyyy-MM-dd HH:mm` 固定显示 24 小时制；原生 `datetime-local` 设为 `lang="zh-CN"` 并保持透明文字层，仅接收系统选择器与键盘输入，变更后由 Blazor 重渲染可视读数。样式通过 `.datetime-control` 外层单边框承载渐变、内描边和焦点状态，避免再次引入 JS 或重复业务状态。

`MI-0049` 在 `DashboardQuerySession.CreateAnalysisQueryAsync` 创建 `ForecastRange` 前，将本地起报时间转换为 UTC 并校验分钟/秒/毫秒均为零；不满足时设置可操作的中文 `AnalysisError` 并终止请求。`ForecastStartHint` 与 `step=3600` 让控件提前表达当前时区的有效分钟，保留原生日期时间输入和缓存 UTC 整点不变量。

`MI-0050` 不再依赖各平台对 `datetime-local step=3600` 的实现差异：`Dashboard.razor` 使用 `input[type=date]` 与 24 个整点 `select` 选项，`OnForecastDateChanged`/`OnForecastHourChanged` 合并当前日期或小时并显式把分钟、秒设为零。`DashboardQuerySession` 和 UTC 缓存边界不变量保持不变。

`MI-0053` 保留日期输入与 `ForecastStartLocal` 状态，将小时选择替换为 Ant Design Blazor `TimePicker<TValue="TimeOnly?">`。组件使用 `Format="HH:00"` 和 `InputReadOnly`，因此弹层只生成 24 个整点小时单列；`OnForecastTimeChanged` 只读取所选小时并重建 `DateTimeKind.Unspecified` 的本地时间，分钟和秒继续显式归零，不引入新的时间状态或 JS 互操作。

`MI-0051` 新增无状态共享组件 `UiIcon.razor`，集中承载本项目使用的 Lucide 路径、`currentColor` 描边和统一 `24×24` 视口；布局、Dashboard 与工作区页面只传入图标名称和样式类，按钮继续由外层元素提供 `title`、`aria-label` 或可见文字。品牌 PNG 仍由 `App.razor`、`MainLayout.razor` 和 manifest 的既有引用消费，替换二进制资产即可同步 Header、SEO、PWA 与设备图标，不引入运行时图片处理。

`MI-0055` 复用 `MarineAnalysisQueryResult.Tide`，由 `DashboardQuerySession` 投影 `DashboardTideResult` 与潮位点、高低潮和涨退潮文本；该投影明确不参与风险评分。`Dashboard.razor` 仅在潮位点存在时动态导入 `tide-chart.js`，模块再从固定版本 `Vizor.ECharts` 静态 Web Asset 加载 ECharts 6；以 SnapshotId 避免重复初始化，通过 `ResizeObserver` 自适应容器，并在重查或页面 Dispose 时销毁实例。JS/Canvas 失败时 Razor 摘要和 Provider 降级文案仍可用。

`MI-0056` 保持 `DashboardQuerySession.IsLoadingAnalysis`、请求取消和重复提交保护不变，仅将条件渲染的忙状态移入 `.query-band`。状态卡使用正常文档流和 scoped CSS，不再采用 `position: fixed` 或页面级 `z-index`；因此查询期间 Header、表单和已有结果继续可见，移动端只通过媒体查询收敛展示密度，不复制业务状态。

## 13. 变更记录

| 版本 | 日期 | 变更说明 |
| --- | --- | --- |
| 1.0 | 2026-07-13 | 定义 Blazor 渲染模式、组件边界、状态和测试策略 |
| 1.1 | 2026-07-13 | 更新为多 Provider 数据源边界 |
| 1.2 | 2026-07-30 | 记录 `MI-0015` Dashboard scoped 状态容器、metrics-only 结果投影和测试覆盖 |
| 1.3 | 2026-07-30 | 记录 `MI-0017` DashboardQuerySession 活动评分、综合结论和风险摘要投影 |
| 1.4 | 2026-07-31 | 记录 `MI-0018` DashboardQuerySession 推荐窗口、风险上升和返航截止投影 |
| 1.5 | 2026-07-31 | 记录 `MI-0019` DashboardQuerySession 趋势、时间带和小时详情状态投影 |
| 1.6 | 2026-07-31 | 记录 `MI-0024` Dashboard 地图/坐标目标状态、Leaflet JS 互操作和失败降级测试 |
| 1.7 | 2026-08-12 | 记录 `MI-0026` 静态 SSR 账户页、认证路由和账户 Header 状态边界 |
| 1.8 | 2026-08-13 | 记录用户工作区页面、单位投影和再次查询参数恢复边界 |
| 1.9 | 2026-08-14 | 记录 `MI-0030` 地图 JS 互操作改用天地图 WMTS 双层瓦片，Key 经配置注入 |
| 2.0 | 2026-08-14 | 记录 `MI-0033` DashboardQuerySession 按浏览器时区持有显示时区、`ForecastStartUtc` 重命名为 `ForecastStartLocal` 并新增 `browser-timezone.js` 懒加载检测，`OnAfterRenderAsync` 首帧校正 |
| 2.1 | 2026-08-14 | 记录 `MI-0034` Dashboard 页面移动端响应式 CSS：逐小时表格次要列标记 `hide-sm`、摘要「时间」项 `summary-span`，`@media (max-width:599px)` 内新增表格收窄、状态带堆叠与触控增强规则 |
| 2.2 | 2026-08-14 | 记录 `MI-0035` 新增静态 SSR 组件 `About.razor`（`@page "/about"`，无 `@rendermode`）与 scoped 样式 `About.razor.css`；`MainLayout.razor` 的 `.app-header` 拆分为 `.app-header-brand`（`.app-brand` + `.main-nav`）+ `<AccountNav />`，`app.css` 新增 `.app-header-brand`/`.main-nav` 布局与移动端换行规则 |
| 2.3 | 2026-08-14 | 记录 `MI-0036` 新增页面组件 `UserLocations.razor`（`@page "/my-locations"`，`[Authorize]` + `InteractiveServer`）与 `AccountNav` 入口「我的地点」；`Login.razor`/`Register.razor` 注入 `CaptchaService` 并在 `OnInitialized` 生成 `_challenge`，以 `MarkupString` 渲染 SVG 验证码；`Dashboard.razor` 增加 `QueryLatitude`/`QueryLongitude` 查询参数恢复自定义坐标；`QueryHistory.razor` 增加 `IJSRuntime` `confirm` 二次确认的删除/清空交互 |
| 2.4 | 2026-08-17 | 记录 `MI-0038` `DashboardQuerySession` 新增 `MapPointName` 状态并把自定义名称经 `MarineAnalysisQuery.DisplayName`（可选，纯展示语义，不参与缓存键/算法）传递到 `Project` 投影；`Dashboard.razor` 新增「地点名称（可选）」输入与 `[SupplyParameterFromQuery(Name="name")]` 回填，地图选点分支收藏星标复用预置地点收藏逻辑；`Favorites.razor` 的 `BuildQueryUrl` 对地图点生成 `/?lat=&lon=&name=` 深链 |
| 2.5 | 2026-08-18 | 记录 `MI-0039` 新增 `AdminTabs`/`AdminLocations`(+`.razor.css`)/`AdminUsers` 三个 `[Authorize(Policy="Administrator")]` + `InteractiveServer` 页面组件；`Operations.razor` 顶部挂 `AdminTabs`；`AdminLocations` 复用 `wwwroot/js/dashboard-map.js` 天地图选点（`EnsureMap`/`SelectMapPoint`）并注入 `IConfiguration` 取 `Map:Tianditu:Key`；`AccountNav.razor` 新增管理员可见「后台管理」入口 |
| 2.6 | 2026-08-19 | 记录 `MI-0040`：`DashboardQuerySession` 新增 `SelectHomeDefaultAsync`（无查询参数时预选首页默认地点，缺省地图收起）、`SetMapError`（可恢复地图提示，不标记永久不可用）；`Dashboard.razor` 「查找地点」/候选点击改为 `SearchLocationsAsync`/`SelectLocationAsync`（命中即打开地图并更新标记跟随显示），`SelectMapPointFromJs` 变 `async` 并注入 `ReverseGeocodeService` 反查最近地名填充 `MapPointName`（带会话选点竞态校验，失败保持空名称），新增 `[JSInvokable] HandleLocateUnavailableFromJs`；`dashboard-map.js` 新增 `addLocateControl`（缩放控件附近的定位十字准星，`getCurrentPosition` 成功后 `map.setView` + 调 `SelectMapPointFromJs`，失败映射中文提示）；`AdminLocations.razor` 表单新增「设为首页默认地点」复选框（经 `CreateLocationCommand.IsHomeDefault` 落库） |
| 2.7 | 2026-08-19 | 记录 `MI-0040` 跟进：逆地理编码从 `TiandituOptions.Key` 拆出独立 `ServerKey`（`Map:Tianditu:ServerKey`），`TiandituReverseGeocoder` 改用服务端权限 Key；浏览器端瓦片仍读取 `Key` |
| 2.8 | 2026-08-19 | 记录 `MI-0041` 品牌/SEO 组件边界：`App.razor` 维护默认搜索与社交元数据、图标和 manifest，各页面通过 `PageTitle` 输出唯一标题，`Dashboard.razor` 维护首页 canonical，`MainLayout.razor` 渲染 Marine AI 品牌组合；`wwwroot` 提供品牌 PNG、robots 与 sitemap |
| 2.9 | 2026-08-19 | 记录 `MI-0043` 呈现层重构：业务状态和事件链保持不变，`MainLayout`/`app.css` 负责全站设计系统与移动底部导航，`Dashboard.razor.css` 负责决策工作台和数据可视化，辅助页面通过 scoped CSS 对齐同一视觉语言 |
| 3.0 | 2026-08-19 | 记录 `MI-0045` 原生日期时间控件封装：不引入新状态或 JS，Razor 增加显示容器与 SVG，scoped CSS 负责暗色 indicator、48px 级点击区域和移动端 16px 输入字体 |
| 3.1 | 2026-08-20 | 记录 `MI-0051` `UiIcon` 共享组件与品牌资产消费边界：集中维护 Lucide 线性 SVG，布局/Dashboard/工作区复用同一组件，Header/SEO/PWA 保持同源 PNG 引用 |
| 3.2 | 2026-08-20 | 记录 `MI-0055` 潮汐投影与 ECharts 互操作：固定 NuGet 静态资产、按需加载、SnapshotId 去重、ResizeObserver、自适应与 Dispose/失败降级 |
| 3.3 | 2026-08-20 | 记录 `MI-0056` Dashboard 查询忙状态边界：复用既有 Session 状态，局部状态卡取代页面级固定遮罩，响应式规则不引入额外业务状态 |
