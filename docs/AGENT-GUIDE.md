# Agent 工作指南与任务台账

> 本文档是 Marine Insight Agent 执行任务、记录进度和跨会话恢复的唯一工作入口。产品与技术范围以各设计文档为准，实时执行状态以本文档为准。

## 1. 核心原则

1. 当前用户的最新明确指令始终优先；不得用历史待办覆盖用户的新要求。
2. 先读取基线、确认工作区和登记任务，再修改代码或文档。
3. RoadMap 负责描述阶段范围，本文档负责记录任务的实际状态、恢复点和验证结果。
4. 同一时间最多一个 `IN_PROGRESS` 任务。未完成任务不得伪装为 `DONE`。
5. 保留用户已有未提交改动；不得擅自回退、删除或重写无关内容。
6. 实现、测试和文档必须同步。设计发生变化时更新对应设计文档，而不只更新本台账。

## 2. 会话启动协议

每次开始仓库任务时按以下顺序执行：

1. 完整读取根目录 `AGENTS.md` 和本文档。
2. 读取 [`README.md`](./README.md)，确认文档目录和当前产品基线。
3. 检查本文档“当前执行状态”和任务清单：
   - 存在与用户指令匹配的 `IN_PROGRESS` 或 `PAUSED` 任务时，从“下一步动作”继续。
   - `BLOCKED` 任务的阻塞条件已经解除时，将其改为 `IN_PROGRESS` 后继续。
   - 用户发起新任务时，创建任务 ID 并登记；若需要切换任务，先为旧任务写清恢复点并标记 `PAUSED`。
4. 检查 Git 工作区，识别用户已有改动，明确本次允许触碰的文件范围。
5. 按“文档读取路由”补读相关文档，确认需求、约束和验收标准。
6. 在首次文件修改前，把当前任务设为 `IN_PROGRESS`，填写目标和下一步动作。

若会话因工具、上下文或环境意外中断，下次 Agent 必须优先读取恢复字段，不得从头重复已完成步骤。

## 3. 文档读取路由

### 3.1 每个实现任务必读

| 文档 | 读取目的 |
| --- | --- |
| [项目概述](./00-项目概述.md) | 确认产品边界、MVP 范围和安全约束 |
| [开发规范](./17-开发规范.md) | 确认工程、编码、注释、测试和文件格式要求 |
| [开发 RoadMap](./21-开发RoadMap.md) | 确认阶段顺序、依赖和退出条件 |
| [测试方案](./20-测试方案.md) | 确认与改动风险相匹配的验证要求 |

纯文档整理任务可按实际影响缩减必读范围，但仍必须读取本文档与文档索引。

### 3.2 按任务类型选读

| 任务类型 | 必读文档 |
| --- | --- |
| 产品范围、用户流程、验收 | [PRD](<./01-产品需求(PRD).md>)、[SRS](<./02-需求规格说明书(SRS).md>) |
| 架构、分层、领域建模 | [SAD](<./03-系统架构设计(SAD).md>)、[DDD](<./04-领域模型设计(DDD).md>) |
| 数据库、迁移、持久化 | [数据库设计](./05-数据库设计.md)、[DDD](<./04-领域模型设计(DDD).md>) |
| API、认证、错误契约 | [API 接口设计](./06-API接口设计.md)、[权限设计](./12-权限设计.md)、[异常处理设计](./14-异常处理设计.md) |
| 天气/海况 Provider、标准化 | [天气数据源设计](./07-天气数据源设计.md)、[缓存设计](./11-缓存设计.md)、[日志设计](./13-日志设计.md)、[异常处理设计](./14-异常处理设计.md) |
| 海况规则、评分、时间窗 | [海况分析引擎设计](./08-海况分析引擎设计.md)、[评分算法设计](./09-评分算法设计.md)、[测试方案](./20-测试方案.md) |
| AI 解读 | [AI 分析引擎设计](./10-AI分析引擎设计.md)、[海况分析引擎设计](./08-海况分析引擎设计.md)、[异常处理设计](./14-异常处理设计.md) |
| Blazor、页面、交互 | [UI 设计](./15-UI设计.md)、[Blazor 组件设计](./16-Blazor组件设计.md)、[API 接口设计](./06-API接口设计.md) |
| 缓存、日志、可观测性 | [缓存设计](./11-缓存设计.md)、[日志设计](./13-日志设计.md)、[异常处理设计](./14-异常处理设计.md) |
| 部署、Docker、运维 | [部署文档](./18-部署文档.md)、[Docker 部署](./19-Docker部署.md)、[日志设计](./13-日志设计.md) |

跨模块任务读取所有受影响模块的文档。不要无目的加载全部文档，也不要在未读相关基线时凭经验实现。

## 4. 任务状态约定

| 状态 | 含义 | 必填信息 |
| --- | --- | --- |
| `TODO` | 已确认但尚未开始 | 任务目标、来源、优先级 |
| `IN_PROGRESS` | 当前正在执行 | 当前进度、下一步动作、涉及文件 |
| `PAUSED` | 被新任务或会话边界暂停，可继续 | 最后完成动作、精确恢复点、剩余工作 |
| `BLOCKED` | 外部条件未满足，当前无法推进 | 阻塞原因、已尝试内容、解除条件 |
| `DONE` | 验收与必要验证均已完成 | 完成日期、验证结果、文档影响 |
| `CANCELLED` | 明确不再执行 | 取消日期、取消原因和决策来源 |

任务 ID 使用 `MI-NNNN`。新增任务取当前最大编号加一；不得复用或重排历史 ID。复选框只表达是否完成，状态列才是任务生命周期的权威字段。

`PAUSED` 和 `BLOCKED` 任务必须在“暂停/阻塞任务恢复详情”中保留独立记录，不能只依赖会被下一项任务覆盖的“当前执行状态”。`CANCELLED` 任务移入“取消任务”，不得直接删除。

## 5. 执行与更新规则

### 5.1 开始任务

1. 在待办清单新增或选择任务。
2. 将状态设为 `IN_PROGRESS`，同时更新“当前执行状态”。
3. 写清本次可验证的目标，不使用“优化一下”“继续开发”等模糊描述。

### 5.2 执行过程中

- 完成关键里程碑后及时更新“最后完成动作”和“下一步动作”。
- 发现新增范围时登记新任务，不把无关改动偷偷并入当前任务。
- 需求或设计有冲突时，先记录冲突与采用的决策，再继续实现。
- 验证失败时保留失败结果；只有修复并重新验证后才能标记完成。

### 5.3 任务中断与恢复

中断前先把任务状态改为 `PAUSED` 或 `BLOCKED`，并在“暂停/阻塞任务恢复详情”创建或更新该任务的独立记录。记录至少包括：

- 已完成到哪个具体步骤。
- 下一条可直接执行的命令或代码修改。
- 已修改和仍需修改的文件。
- 已执行的验证及其结果。
- 未解决风险、阻塞条件和需要用户确认的决策。

下次会话恢复时：

1. 先检查工作区是否与记录一致。
2. 从“下一步动作”继续，不重复已验证通过的步骤。
3. 若记录与实际文件不一致，以文件和 Git 差异为证据修正台账。
4. 若用户的新指令与旧任务冲突，最新指令优先；旧任务保留为 `PAUSED`，不得静默丢失。

## 6. 会话收尾协议

每次项目任务结束、暂停或阻塞前，Agent 必须在最终回复前完成：

1. 更新“当前执行状态”和任务清单，确保状态与实际结果一致。
2. 完成则移入已办清单，取消则移入取消任务；未完成则保留为 `IN_PROGRESS`、`PAUSED` 或 `BLOCKED` 并写清恢复点。
3. 追加一条“会话记录”，写明结果、验证和下一步。
4. 同步本次影响的需求、设计、API、数据库、测试或部署文档。
5. 检查 Git 差异，确认没有覆盖用户改动，也没有混入无关文件。
6. 对本次改动过的文本文件执行 UTF-8 with BOM 与 CRLF 检查；JSON、YAML 等现代配置文件按其惯例处理。
7. 运行与风险匹配的构建、测试或文档链接检查，并记录未能执行的验证。

纯问答、明确的只读审查或用户明确禁止写入时，不为记录会话而擅自修改仓库；最终回复中应说明本次未更新台账。其他项目任务必须更新本文档。

## 7. 当前执行状态

<!-- agent-state:start -->

| 字段 | 当前值 |
| --- | --- |
| 当前任务 ID | `MI-0027` |
| 当前状态 | `BLOCKED` |
| 当前目标 | 一次性完成阶段 3 剩余产品闭环与阶段 4 可部署基线：收藏/历史/单位设置、移动端可访问性、管理员运维视图、WorldTides 可配置降级、容器化、备份恢复和上线验证 |
| 最后完成动作 | `MI-0028` AI 解读引擎已闭环：OpenAI 兼容适配器、事实/安全校验、24 小时缓存和规则模板降级已落地，API/Dashboard 均投影 `explanation`；WorldTides Key 已写入当前用户的 .NET User Secrets 并启用，WebApplicationFactory 和 Playwright 均强制关闭真实 Provider 与 AI，防止自动化测试消耗付费额度 |
| 下一步动作 | AI 真实联调由用户自验：`scripts/configure-ai-secret.ps1` 配置 `AI:ApiKey`+`Enabled=true` 后查询应返回 `source=ai`，断网/超时应自动降级 `degraded=true`；MI-0027 仍待人工受控 WorldTides 查询与 Docker/Staging 外部验证 |
| 涉及文件 | Application/Infrastructure/Web 的 AI 解读与用户工作区、WorldTides、独立 PostgreSQL 迁移项目、Docker/Caddy/Secret、运维脚本、CI、Playwright 测试及数据库/API/Provider/UI/部署/测试/RoadMap 文档 |
| 验证结果 | Release 构建 0 警告、0 错误；.NET 测试基线 161 个（Domain 51、Application 47、Infrastructure 32、Web 31）全量通过；AI 适配器错误映射、事实校验和降级路径已覆盖；测试宿主显式关闭 AI 与 WorldTides，无真实付费调用 |
| 阻塞/待确认 | 当前机器仍无 Docker CLI，不能验证 Docker Secret 注入及真实 Compose/PostgreSQL/代理/备份恢复；真实 WorldTides 请求会消耗 Credit，本次未自动调用；后台 Web 启动探针被环境策略阻止 |
| 最后更新 | 2026-08-13 |

<!-- agent-state:end -->

## 8. 未完成任务

### 8.1 待办任务

| 完成 | ID | 优先级 | 状态 | 任务 | 来源与验收 |
| --- | --- | --- | --- | --- | --- |
| [ ] | `MI-0027` | P0 | `BLOCKED` | 连续完成阶段 3 剩余产品闭环与阶段 4 可部署基线 | 注册用户可收藏/再次查询并查看历史与单位设置；移动端、键盘和管理员运维入口可用；WorldTides 可配置且凭据安全维护；Docker、代理、迁移、备份恢复、安全与 E2E 验证具备可执行交付物 |
用户自行处理的 `MI-0002`、`MI-0003`、`MI-0004` 不纳入本 Agent 实施范围。

### 8.2 暂停/阻塞任务恢复详情

#### `MI-0027` / `BLOCKED`

| 字段 | 当前值 |
| --- | --- |
| 任务目标 | 完成阶段 3 产品闭环和阶段 4 可部署、可恢复、可验证基线 |
| 最后完成动作 | 本地实现、设计文档、138 个 .NET 测试和桌面/移动 Playwright 均已完成；Playwright Chromium 与 WorldTides User Secrets 已就绪；自动化测试显式隔离真实付费 Provider；部署与恢复资产已生成 |
| 下一步动作 | 可直接执行 `npm run test:e2e`；人工受控执行一次真实 WorldTides 查询；在 Docker/Staging 使用 `compose.worldtides.yaml` 和仓库外 Secret，继续容器、PostgreSQL、代理、冒烟及备份恢复演练 |
| 涉及文件 | `Dockerfile`、`compose.yaml`、`deploy/`、`scripts/`、`src/MarineInsight.Migrations.PostgreSql/`、WorldTides/用户工作区/Web/测试和相关设计文档 |
| 验证结果 | Release 构建 0 警告/0 错误；.NET 基线 138，Web 30/30、WorldTides 契约 2/2 通过；User Secrets 和仓库隔离验证通过；Chromium `151.0.7922.34` 已通过 Playwright API 启动验证；Docker/PostgreSQL/代理/恢复和真实 WorldTides 请求尚未执行 |
| 阻塞与解除条件 | 浏览器和 WorldTides 本地凭据环境已就绪；剩余解除条件为可访问的 Docker/Staging 环境，并在明确接受 Credit 消耗后执行一次真实 WorldTides 联调。NuGet 审计源需恢复稳定网络后重跑 |
| 最后更新 | 2026-08-13 |

出现暂停或阻塞任务时，每个任务保留一个独立详情块，使用以下字段：

| 字段 | 记录要求 |
| --- | --- |
| 任务 ID / 状态 | `MI-NNNN`，状态只能是 `PAUSED` 或 `BLOCKED` |
| 任务目标 | 本任务最终要达到的可验证结果 |
| 最后完成动作 | 已完成到的具体步骤，不写模糊进度 |
| 下一步动作 | 下次可直接执行的命令、文件修改或验证 |
| 涉及文件 | 已修改以及仍需修改的路径 |
| 验证结果 | 已运行命令、成功/失败结果和未运行项 |
| 阻塞与解除条件 | 阻塞原因、已尝试内容、需要谁提供什么条件 |
| 最后更新 | `YYYY-MM-DD` |

## 9. 已办与取消任务

### 9.1 已办任务

| 完成 | ID | 完成日期 | 任务 | 验证与说明 |
| --- | --- | --- | --- | --- |
| [x] | `MI-0001` | 2026-07-13 | 建立 Agent 主引导、任务状态与跨会话恢复机制 | 根目录自动入口和 docs 主台账已建立；本地链接、Git 差异、UTF-8 BOM 与 CRLF 已验证 |
| [x] | `MI-0005` | 2026-07-15 | 建立 .NET 10 解决方案、分层项目、测试项目、EditorConfig 与 CI | `MarineInsight.slnx` 包含 4 个运行项目和 4 个测试项目；依赖方向符合 SAD；构建、4 个基础测试、格式、差异和 Web HTTP 检查通过 |
| [x] | `MI-0006` | 2026-07-15 | 定义 Provider 端口、标准预报模型、质量状态和错误模型 | Domain 标准模型和不变量、Application Weather/Marine/Tide 端口、Provider 错误层及契约测试已完成；构建和 14 个测试通过 |
| [x] | `MI-0007` | 2026-07-15 | 建立 PostgreSQL/SQLite 基础配置和首个迁移 | Infrastructure 已接入 EF Core 10、SQLite/PostgreSQL Provider、四张预报存储表和首个迁移；Web 已按配置注册 DbContext；SQLite 迁移及两种 Provider 选择测试通过；未执行真实 PostgreSQL 集成迁移；Web host 进程探针受当前 PowerShell 环境限制未完成 |
| [x] | `MI-0008` | 2026-07-15 | 建立健康检查、结构化日志和 OpenTelemetry Trace | Web 提供 `/health/live`、`/health/ready` 和数据库有界连接检查；Serilog 输出结构化 JSON 并脱敏 API Key/Token/Authorization/密码/连接字符串/精确位置；接入 W3C Trace、ASP.NET Core/HttpClient/Runtime instrumentation，OTLP 地址可选；设计、部署和 Docker 文档已同步 |
| [x] | `MI-0009` | 2026-07-16 | 实现 Open-Meteo Weather/Marine Provider、DTO、防腐映射、质量校验和契约测试 | Weather/Marine 使用独立批次；请求和响应统一 UTC、单位和方向，保留实际网格坐标；缺失、模型不支持、非法值、契约错误和 HTTP 故障分别处理；固定 JSON、错误路径、超时、DI 和真实只读接口核对通过；天气/部署文档已同步 |
| [x] | `MI-0010` | 2026-07-16 | 实现 ForecastSnapshot 领域模型、UTC 时间轴组装和显式来源选择策略 | 新增 Snapshot/Point/Quality/SourceBatchReference；Assembler 支持 Weather/Marine/Tide 独立批次、最大最近点差、同域批次选择、同指标选择、来源实际时间和 Stale/Partial/Unknown/TimeGap 传递；Web 已注册组装器，DDD/数据源/测试文档已同步 |
| [x] | `MI-0011` | 2026-07-16 | 实现 ForecastBatch 持久化查询边界，以及 24/72/168 小时预报批次的保存和读取用例 | Application 增加 `IForecastBatchRepository`；Infrastructure 追加写入并按地点/Provider/数据域/UTC 覆盖范围读取，完整映射点位、质量、缺失指标和逐指标来源；注册 Scoped 仓储并同步 DDD/数据库/测试/RoadMap 文档 |

| [x] | `MI-0012` | 2026-07-16 | 建立标准预报 L1 缓存边界、版本化缓存键、单航班回源和 Stale 降级 | Application 增加 `ForecastCacheKey`、`ForecastCachePolicy`、`IForecastBatchCache` 和 `ForecastBatchCacheCoordinator`；Infrastructure 接入 `IMemoryCache`、配置校验、键工厂和 Web DI；ProviderException 仅在窗口内回退旧值并将 Stale 质量传递到批次/点位/来源，缓存后端故障旁路；同步缓存、部署、测试和 RoadMap 文档 |
| [x] | `MI-0013` | 2026-07-16 | 建立 `POST /api/v1/marine-analyses` 指标与质量摘要查询骨架 | Application 并行查询 Weather/Marine 并通过 L1 缓存组装 ForecastSnapshot；Web 返回 metrics-only 标准指标、逐小时质量、指标来源、批次来源和 `hit`/`miss`/`stale`；坐标与 24/72/168 小时校验返回 `VALIDATION_FAILED`，Provider 故障返回 `PROVIDER_UNAVAILABLE` ProblemDetails；补充 4 个 API 集成场景和 DI 注册；评分、活动、地点解析和持久化留待后续；同步 API/异常/测试/RoadMap 文档 |
| [x] | `MI-0014` | 2026-07-16 | 建立地点目录查询边界和 `locationId` 分析输入 | Domain 增加 Location/LocationType 不变量；Application 增加查询端口和参数校验；Infrastructure 增加 AsNoTracking 搜索、Haversine 附近排序、三条预置地点种子和 migration；Web 提供 `/locations/search`、`/locations/nearby`，分析请求支持地点 ID、时区/名称投影和 `LOCATION_NOT_FOUND`；不接入外部地理编码和收藏 |
| [x] | `MI-0015` | 2026-07-30 | 实现基础 Blazor Dashboard 查询闭环 | Web 根路径切换为海况 Dashboard；新增 `DashboardQuerySession` 管理地点搜索、候选选择、请求取消和 metrics-only 查询投影；页面展示来源状态、抓取/发布时间、缓存状态、质量、关键指标和逐小时指标表；不实现评分、活动建议、地图、收藏或登录；同步 UI/Blazor/测试/RoadMap 文档 |
| [x] | `MI-0016` | 2026-07-30 | 建立领域层 Safety Gates 与基础评分骨架 | Domain 新增 `RiskLevel`、`RiskContribution`、`HourlyMarineAssessment` 和 `MarineRiskRuleEngine`；支持单小时综合分、Avoid/Unknown 不变式、算法版本、置信度和风险贡献；覆盖雷暴/大风/大浪/强阵风/低能见度 Gate、基础惩罚、风小浪大、阵风异常、短周期浪、长周期涌浪和海况关键数据缺失；暂不接入 API/Dashboard/Activity Profile |
| [x] | `MI-0017` | 2026-07-30 | 实现 Activity Profile 与逐小时活动评分 API/Dashboard 闭环 | Domain 新增 `ActivityType`、`ActivityProfile`、`ActivityMarineAssessment` 和活动评分服务；Application 生成逐小时综合/活动评估；API 返回 analyzed、overall、activities、risks 和 hourly assessment 投影并校验未知活动；Dashboard 展示综合结论、活动评分、主要风险和逐小时评分表；不实现推荐时间窗、返航截止、趋势 Tabs、小时详情或截图验证 |
| [x] | `MI-0018` | 2026-07-31 | 实现推荐时间窗、风险快速上升和返航截止闭环 | Domain 新增 `RecommendationWindow` 和 `MarineRecommendationWindowPlanner`；Application 查询结果携带推荐窗口；API 返回 `recommendedWindows`；Dashboard 展示推荐窗口、风险上升提示和保守返航截止；不实现趋势图 JS、小时详情、地图、AI、潮汐或参数后台 |
| [x] | `MI-0019` | 2026-07-31 | 实现 Dashboard 趋势 Tabs、推荐窗口时间带和小时详情面板 | Web 状态容器新增趋势点、时间带、小时详情和选中小时；Dashboard 支持分数/风/浪趋势切换、推荐窗口横向时间带、点击小时查看完整指标/风险/来源摘要；不引入第三方 JS 图表、地图、AI、潮汐或收藏 |
| [x] | `MI-0020` | 2026-07-31 | 建立 GS-001 至 GS-010 海况黄金样本回归测试 | Domain 新增黄金样本测试，覆盖平静海况、风小浪大、阵风突增、短周期颠簸、长周期岸边风险、雷暴、CAPE、低能见度、关键海况缺失和 17:00 后风险上升窗口；同步评分算法、测试方案和 RoadMap 文档 |
| [x] | `MI-0021` | 2026-07-31 | 建立算法参数 Schema、版本实体和发布前校验 | Domain 新增 `MarineAlgorithmParameters`、参数 Schema 版本、配置哈希、`AlgorithmParameterValidator`、发布校验结果和 `AlgorithmVersion` 状态机；发布前校验覆盖 Safety Gate、分段惩罚、组合规则、活动 Profile、置信度、推荐窗口和黄金样本门禁；不包含后台 UI、数据库迁移或真实发布接口 |
| [x] | `MI-0022` | 2026-07-31 | 实现缓存键与算法版本联动 | Application 新增 `MarineAnalysisCacheIdentity`，将来源批次集合哈希、来源选择策略、算法版本和归一化活动集合写入分析语义键和 ETag；查询结果携带缓存身份；API 返回根级 `algorithmVersion`、`cache` 对象、活动级算法版本、`ETag` 响应头并支持 `If-None-Match` 返回 `304`；不包含 Redis L2、分析结果持久化或运行时动态参数切换 |
| [x] | `MI-0023` | 2026-07-31 | 评审并校准初始阈值 | 对照官方大风、海浪和大雾预警口径评审 `marine-score-1.0.0` 初始阈值；将阵风 Safety Gate 从 18 m/s 校准为 17.2 m/s，并同步默认参数 Schema、惩罚分段和边界测试；浪高 2.0 m、能见度 < 500 m 保持保守边界；不接入实时官方预警 API、潮汐或岸线暴露规则 |
| [x] | `MI-0024` | 2026-07-31 | 实现 Leaflet/OpenStreetMap 地图选点与失败降级 | Dashboard 增加全宽 Leaflet/OpenStreetMap 地图选点、可见 OSM 署名、瓦片/脚本失败提示和经纬度输入降级；`DashboardQuerySession` 支持预置地点和自定义坐标两类提交目标并复用同一分析流程；不做离线瓦片、瓦片代理、收藏/登录、实时定位或截图验证 |
| [x] | `MI-0025` | 2026-08-12 | 消除 SQLite 原生库 High 严重性漏洞 | EF Core/Design/SQLite 统一升级到 `10.0.11`，传递依赖解析为 `SQLitePCLRaw 2.1.12`；NuGet 漏洞审计无报告，Release 构建、SQLite 迁移/仓储测试和全量 124 个测试通过 |
| [x] | `MI-0026` | 2026-08-12 | 实现 ASP.NET Core Identity 基础认证闭环 | 新增 UUID Identity 用户/角色迁移、注册/登录/退出、静态 SSR 账户页、Header 认证状态、Secure Cookie、密码/锁定、防伪、账户限流和 5 个认证集成测试；匿名 Dashboard/API 保持可用 |
| [x] | `MI-0028` | 2026-08-13 | 实现 AI 解读引擎（OpenAI 兼容协议） | Application 定义 `IExplanationProvider`/`IExplanationCache` 端口、`ExplanationService` 编排、规则模板和事实/安全校验；Infrastructure 提供 OpenAI 兼容适配器、`IMemoryCache` 缓存和 `AI` 配置校验；Web 的 API 与 Dashboard 均投影 `explanation`，AI 关闭/失败降级为模板；密钥脚本与 `.secrets` 文档同步；新增 23 个自动化用例 |
| [x] | `MI-0029` | 2026-08-13 | 实现分析结果持久化（AnalysisReport） | Domain 新增 `AnalysisReport` 聚合根、`AnalysisRisk`/`AnalysisSourceBatch` 值对象和 `AnalysisSourceRole` 枚举；Application 定义 `IAnalysisReportRepository` 端口、`AnalysisReportAssembler` 投影和 `AnalysisReportService` 编排（属主校验）；Infrastructure 提供三张表实体/配置/手动映射和仓储，SQLite 与 PostgreSQL 双迁移；Web 在 Dashboard 认证分支落库并提供 `GET /api/v1/marine-analyses/{id}` 属主读取端点；匿名查询不落库，历史对比与 `algorithm_versions` 外键升级留待后续 |

### 9.2 取消任务

当前没有 `CANCELLED` 任务。取消任务必须保留任务 ID、取消日期、原任务目标、取消原因和用户/决策来源。

## 10. 会话记录

按时间倒序追加，保留最近 30 条；清理旧记录时不得删除待办、已办和关键决策。

| 日期 | 任务 ID | 会话结果 | 验证 | 下一步 |
| --- | --- | --- | --- | --- |
| 2026-08-13 | `MI-0029` | 完成分析结果持久化闭环：Domain 落地 `AnalysisReport` 聚合根、`AnalysisRisk`/`AnalysisSourceBatch` 值对象和 `AnalysisSourceRole` 枚举；Application 新增 `IAnalysisReportRepository` 端口、`AnalysisReportAssembler` 投影和 `AnalysisReportService` 编排（属主校验）；Infrastructure 新增三张表实体/配置/手动映射和仓储，生成 SQLite 与 PostgreSQL 双迁移并刷新幂等 SQL；Web 在 Dashboard 认证分支落库并新增 `GET /api/v1/marine-analyses/{id}` 属主读取端点；匿名查询不落库 | Release 构建 0 警告、0 错误；全量测试 173/173 通过（Domain 51、Application 51、Infrastructure 36、Web 35）；`dotnet format --verify-no-changes`、`git diff --check` 通过，34 个改动文本文件 BOM/CRLF 已统一；未执行真实 PostgreSQL 迁移和登录后落库人工验证 | 历史对比（多结果并排）、`algorithm_versions` 外键升级（MI-0021 仓储落地后）、分析结果清理 Job 留待后续；真实 PostgreSQL/登录落库由用户自验 |
| 2026-08-13 | `MI-0028` | 完成 AI 解读引擎闭环：Application 新增解释端口、事实 DTO、规则模板、事实/安全校验器和 `ExplanationService` 编排；Infrastructure 新增 OpenAI 兼容适配器、`AI` 配置校验和 `IMemoryCache` 缓存；Web 的 API 与 Dashboard 均投影 `explanation`，AI 默认关闭且失败一律降级模板；修复 `AI:CacheLifetime` 用 `"24:00:00"` 被 `TimeSpan` 解析为 24 天的问题，改为 `"1.00:00:00"` | .NET 全量测试 161/161 通过（Application 47、Domain 51、Infrastructure 32、Web 31）；AI 适配器 401/403/429/5xx/非法 JSON/超时错误映射与事实校验/降级路径均已覆盖；测试宿主显式关闭 AI，无真实模型调用 | 真实 Key 联调由用户自验：`scripts/configure-ai-secret.ps1` 配置 `AI:ApiKey`+`Enabled=true` 后查询应返回 `explanation.source=ai`；断网/超时应自动降级 `degraded=true`；MI-0027 仍等待 Docker/Staging 与真实 WorldTides 外部验证 |
| 2026-08-13 | `MI-0027` | 完成 WorldTides 密钥维护设计和本机配置：真实 Key 仅存入当前用户的 .NET User Secrets；增加安全提示式配置/删除脚本、可选 Compose 外部 Secret、Git/Docker 排除规则、CI/E2E 付费调用隔离和轮换/泄露处置文档；未把明文写入仓库或输出 | User Secrets 中 `Enabled=true` 且 Key 非空；仓库跟踪内容和当前差异无明文 Key；本地/部署 Secret 路径均被 Git 忽略；Release 构建 0 警告/0 错误，Web 30/30、WorldTides 契约 2/2、格式和 PowerShell AST 通过；后台启动探针被策略阻止，未发出真实 WorldTides 请求 | 明确接受一次 Credit 消耗后做真实潮汐/Credit 联调；Docker 可用后用 `compose.worldtides.yaml` 验证 Key-per-file 注入；由于 Key 曾出现在对话中，若对话可能共享或长期保留，应在供应商控制台轮换 |
| 2026-08-13 | `MI-0027` | 代理网络调整后重新安装 Playwright 管理的 Chromium，浏览器测试环境阻塞已解除；未修改业务代码 | `npx playwright install chromium` 成功安装 Chromium、Headless Shell、FFmpeg 和 Winldd；Playwright `1.62.1` 默认浏览器路径存在，并通过 API 成功启动 Chromium `151.0.7922.34` 后正常关闭 | 下次直接执行 `npm run test:e2e`，无需设置 `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH`；任务仍等待 Docker/Staging 和 WorldTides API Key 完成外部发布验证 |
| 2026-08-13 | `MI-0027` | 从提交 `ef0a673` 恢复并完成本地收尾：确认该提交已实现收藏/历史/设置、运维视图、WorldTides 和用户工作区迁移；随后修复 SQLite 时间排序、再次查询/单位/活动恢复、Dashboard SSR 和资源错误，补齐 WorldTides 降级契约、Docker/Caddy/Secret、PostgreSQL 专用迁移、备份恢复/冒烟脚本、CI E2E 和设计文档；因外部发布验证条件不足转为 `BLOCKED` | Release 构建 0 警告、0 错误；Domain 51、Application 29、Infrastructure 28、Web 29，合计 137/137 通过；本机 Chrome 的桌面/移动 Playwright 2/2 通过；`dotnet format`、PowerShell AST、npm audit 0 漏洞、`git diff --check` 和 BOM/CRLF 检查通过；NuGet 在线审计因 TLS EOF 未完成；Docker CLI 不可用，真实 PostgreSQL/代理/备份恢复未执行；无 WorldTides API Key，未执行付费联调 | 提供 Docker/Staging 后执行完整 Compose、PostgreSQL、代理、备份恢复和冒烟演练；提供 WorldTides Key 后执行真实 Credit/潮汐联调，再评估阶段 4 准出 |
| 2026-08-12 | `MI-0026` | 完成 ASP.NET Core Identity 基础认证闭环；统一用户/角色存储到现有 DbContext，增加注册、登录、退出、账户页面与 Header 状态，落实 Secure/HttpOnly/SameSite Cookie、密码策略、失败锁定、防伪、IP 限流和本地重定向防护；邮箱确认在邮件链路完成前保持关闭；同步权限、数据库、API、UI、Blazor、部署、测试和 RoadMap | Release 构建 0 警告、0 错误；迁移测试 3/3、认证测试 5/5、全量测试 129/129 通过；NuGet 漏洞审计无报告；独立临时 SQLite 全量迁移与 HTTPS `/`、`/account/login`、`/account/register`、`/health/live` 检查通过；`dotnet format --verify-no-changes`、`git diff --check`、JSON 解析及 28 个改动文本文件 BOM/CRLF 检查通过；Playwright 未安装，未执行浏览器视觉/360px 自动化 | 新建 `MI-0027`：实现登录用户收藏地点与一键再次查询闭环，覆盖重复收藏和所有权隔离 |
| 2026-08-12 | `MI-0025` | 完成 SQLite 原生依赖 High 漏洞修复；EF Core、Design 和 SQLite 从 `10.0.9` 统一升级到 `10.0.11`，由官方补丁依赖链将 SQLitePCLRaw 从 `2.1.11` 提升到 `2.1.12`；同步测试方案和 RoadMap | NuGet 全解决方案传递依赖漏洞审计无报告；Release 构建 0 警告、0 错误；SQLite 迁移/仓储定向测试 9/9、全量测试 124/124 通过；格式、差异和 4 个改动文本文件 BOM/CRLF 检查通过 | 继续阶段 3：先实现 ASP.NET Core Identity 基础认证，再实现登录用户收藏地点和一键再次查询闭环 |
| 2026-07-31 | `MI-0024` | 完成 Leaflet/OpenStreetMap 地图选点与失败降级；Dashboard 可点击地图或输入经纬度选择自定义坐标并进入同一海况分析流程，地图脚本或瓦片失败时保留坐标输入；页面展示 OSM 署名；同步 UI、Blazor、测试方案和 RoadMap 文档 | Web 测试 23/23 通过；默认 Debug 构建 0 错误；Release 构建 0 错误；全量测试 124/124 通过；`dotnet format --verify-no-changes --no-restore` 通过；一次并行运行 Debug 构建和 Release 测试曾因共享 `obj` 目录出现 `ref` 元数据文件竞争，串行重跑通过；仍有 NU1903 SQLite 警告；按用户指令未执行截图验证 | 建议继续阶段 3：ASP.NET Core Identity、收藏地点、查询历史和用户单位设置；或移动端 360px 导航、键盘和基础可访问性 |
| 2026-07-31 | `MI-0023` | 完成初始阈值官方预警口径评审；阵风 Safety Gate 从 18 m/s 下调到 17.2 m/s，默认参数 Schema 和 `windGustMs` 惩罚分段同步；补充 17.1/17.2 m/s 边界测试；评分算法文档记录大风、海浪和大雾口径依据，SRS/分析/测试/RoadMap 同步 | Domain 测试 51/51 通过；Release 构建 0 错误；全量测试 122/122 通过；`dotnet format --verify-no-changes`、`git diff --check` 和 9 个改动文本文件 BOM/CRLF 检查通过；仍有 NU1903 SQLite 漏洞警告 | 建议进入阶段 3：优先做 Leaflet/OpenStreetMap 全宽选点与地图失败降级；收藏/历史/用户单位设置和 WorldTides 潮汐可作为后续阶段 3 任务 |
| 2026-07-31 | `MI-0022` | 完成缓存键与算法版本联动；新增分析缓存身份值对象，稳定生成来源批次集合哈希、来源选择策略、算法版本和活动集合语义键/ETag；Application 查询结果携带缓存身份；API 返回根级算法版本、`cache` 对象、活动算法版本和 `ETag`，并支持相同 `If-None-Match` 返回 `304`；同步缓存、API、测试和 RoadMap 文档 | Application 测试 26/26 通过；Web 测试 21/21 通过；Release 构建 0 错误；全量测试 120/120 通过；`dotnet format --verify-no-changes`、`git diff --check` 和 13 个改动文本文件 BOM/CRLF 检查通过；首次并行测试因输出文件锁出现一次构建失败，已改为串行测试通过；仍有 NU1903 SQLite 漏洞警告 | 建议继续阶段 2 最后一项：用经验样本和官方预警案例评审初始阈值；或进入阶段 3 地图/收藏/潮汐等产品完善任务 |
| 2026-07-31 | `MI-0021` | 完成算法参数 Schema、版本实体和发布前校验；Domain 新增参数集合、Safety Gate/置信度/推荐窗口/组合规则/惩罚分段值对象、配置哈希、校验结果和 `AlgorithmVersion` 状态机；补充默认参数可发布、哈希篡改、缺少黄金样本、非法阈值/区间、活动 Profile 缺失和状态流转测试；同步 DDD、分析、评分、测试和 RoadMap 文档 | Domain 测试 49/49 通过；全量测试 118/118 通过；构建 0 错误；`dotnet format --verify-no-changes --no-restore` 和 `git diff --check` 通过；仍有 NU1903 SQLite 漏洞警告 | 建议继续阶段 2 缓存键与算法版本联动；管理员后台、仓储、审计和运行时动态参数切换留待后续任务 |
| 2026-07-31 | `MI-0020` | 完成 `GS-001` 至 `GS-010` 海况黄金样本回归测试；新增 `MarineGoldenSampleTests`，用真实领域规则引擎、活动评分服务和推荐窗口规划器回放黄金样本，锁定规则代码、Safety Gate、活动分差异、Unknown 和返航缓冲；同步评分算法、测试方案和 RoadMap 文档 | Domain 测试 39/39 通过；全量测试 108/108 通过；构建 0 错误；`dotnet format --verify-no-changes --no-restore`、`git diff --check` 和 5 个非 JSON/YAML 文本 BOM/CRLF 检查通过；仍有 NU1903 SQLite 漏洞警告 | 建议继续阶段 2 算法参数 Schema/版本实体与发布前校验，或进入阶段 3 前做用户人工视觉/可访问性验收；不提前进入地图、AI、潮汐或收藏 |
| 2026-07-31 | `MI-0019` | 完成 Dashboard 趋势 Tabs、推荐窗口时间带和小时详情面板；`DashboardQuerySession` 投影分数/风/浪三组趋势、推荐窗口时间带、默认选中小时和完整小时详情；页面支持趋势切换与逐小时行选择；同步 UI、Blazor、测试和 RoadMap 文档 | Web 测试 21/21 通过；全量测试 98/98 通过；构建 0 错误；仍有 NU1903 SQLite 漏洞警告；按用户指令未执行截图验证 | 建议继续阶段 2 黄金样本集、算法参数 Schema/版本实体，或先做用户人工视觉/可访问性验收；不提前进入地图、AI、潮汐或收藏 |
| 2026-07-31 | `MI-0018` | 完成推荐时间窗、风险快速上升和保守返航截止闭环；新增领域窗口值对象与规划服务，按活动最低分、置信度、Safety Gate 和连续小时生成窗口；Application/API/Dashboard 已投影 `recommendedWindows`、返航截止、风险上升点和风险原因；同步分析、评分、DDD、API、UI、Blazor、测试和 RoadMap 文档 | Domain 测试 29/29 通过；全量测试 98/98 通过；构建 0 错误；`dotnet format --verify-no-changes --no-restore` 和 `git diff --check` 通过；仍有 NU1903 SQLite 漏洞警告；按用户指令未执行截图验证 | 建议继续阶段 2 趋势 Tabs、图表时间带、小时详情抽屉或黄金样本集；不提前进入阶段 3 |
| 2026-07-30 | `MI-0017` | 完成 Activity Profile 与逐小时活动评分 API/Dashboard 闭环；新增五类活动默认乘数、活动评分值对象和服务；Application 查询结果携带逐小时评估；API 响应升级为 `analyzed` 并返回综合分、活动分和风险贡献；Dashboard 展示综合结论、活动评分、主要风险和逐小时评分表；同步分析、评分、DDD、API、UI、Blazor、测试和 RoadMap 文档 | Domain 测试 25/25 通过；全量测试 94/94 通过；构建 0 错误；`dotnet format --verify-no-changes --no-restore` 和 `git diff --check` 通过；本次 28 个非 JSON/YAML 文本 BOM/CRLF 检查通过；仍有 NU1903 SQLite 漏洞警告；按用户指令未执行截图验证 | 建议继续阶段 2 推荐时间窗、风险快速上升识别、返航截止、趋势 Tabs 和小时详情；不提前进入阶段 3 |
| 2026-07-30 | `MI-0016` | 完成领域层单小时 Safety Gates 与基础评分骨架；新增 `MarineRiskRuleEngine`、等级/严重度/贡献/评估值对象和 13 个 Domain 测试；同步分析引擎、评分算法、DDD、测试和 RoadMap 文档；不改变现有 metrics-only API/Dashboard 行为 | Domain 测试 23/23 通过；全量测试 91/91 通过；构建 0 错误；`dotnet format`、`git diff --check` 和本次 13 个非 JSON/YAML 文本 BOM/CRLF 检查通过；并发构建/测试曾因输出文件锁出现一次构建失败，单独重跑通过；仍有 NU1903 SQLite 漏洞警告 | 建议继续实现 Activity Profile，并把活动分数、风险贡献和算法版本投影到 API/Dashboard |
| 2026-07-30 | `MI-0015` | 完成基础 Blazor Dashboard 查询闭环；根路径进入 Dashboard，支持地点搜索候选、UTC 起报时间、24/72/168 小时范围、metrics-only 查询提交、来源状态、关键指标和逐小时表格；用户明确接手人工视觉验证，本次不执行截图验证 | 构建 0 错误；全量测试 78/78 通过；`dotnet format`、`git diff --check`、12 个非 JSON/YAML 文本 BOM/CRLF 检查通过；本地 `/`、`/health/live` 和地点搜索 HTTP 200；仍有 NU1903 SQLite 漏洞警告 | 等待用户人工验证 Dashboard；后续按用户指令进入阶段 2 评分/活动分析或继续补 UI/P1 能力 |
| 2026-07-16 | `MI-0014` | 从中断点恢复并完成地点功能的运行验证；Development Web Host 显式加载 SQLite 配置，应用初始存储和预置地点迁移，确认地点搜索、附近排序和健康端点可通过真实 HTTP 访问；清理探针进程后重新执行测试；不处理 `MI-0002` 至 `MI-0004` | `/health/live` 200；`/api/v1/locations/search?q=东极岛` 200；`/api/v1/locations/nearby?lat=30.194&lon=122.687&radiusKm=500&limit=3` 200；地点定向测试 3/3、全量测试 75/75 通过；`git diff --check` 通过；启动时的无迁移 500 已通过执行 EF 迁移解决；仍有 NU1900、NU1903 警告 | 等待用户指定下一项；不处理 `MI-0002` 至 `MI-0004` |
| 2026-07-16 | `MI-0014` | 完成地点目录查询任务；新增 Location 领域模型、Application 查询边界、EF 只读仓储、预置数据迁移、地点搜索/附近 API，并让 metrics-only 分析支持 `locationId` 与地点元数据；同步 API/DDD/数据库/异常/测试/RoadMap 文档；不处理 `MI-0002` 至 `MI-0004` | 构建 0 错误；全量测试 75/75 通过；`dotnet format --verify-no-changes`、`git diff --check`、31 个非 JSON/YAML 文本 BOM/CRLF 检查通过；仍有 NU1900、NU1903 警告；未连接真实 PostgreSQL/Redis | 等待用户指定下一项；不处理 `MI-0002` 至 `MI-0004` |
| 2026-07-16 | `MI-0014` | 启动地点目录查询任务；核对 PRD/SRS、DDD、数据库和 API 契约，确认已有 `locations` 表但没有地点查询端口或 API；范围限定为只读预置/附近查询和分析 `locationId` 输入，不接入外部地理编码或收藏 | 尚未运行本任务验证；基线 MI-0013 为构建 0 错误、63/63 测试通过 | 实现 Location 领域模型、查询端口/仓储、地点 API、`locationId` 分析解析和对应测试；不处理 `MI-0002` 至 `MI-0004` |
| 2026-07-16 | `MI-0013` | 完成 metrics-only 海况分析查询骨架；Weather/Marine 并行回源，串联版本化缓存键、L1 缓存和 ForecastSnapshot，投影指标、质量、来源和缓存状态；补充验证错误、Provider 错误和 Trace-Id；不实现评分、活动、地点名称解析和分析持久化 | `dotnet build` 0 错误；全量 `dotnet test` 63/63 通过；仍有 NU1900、NU1903 警告；真实 PostgreSQL/Redis 未连接 | 等待用户指定下一项；不处理 `MI-0002` 至 `MI-0004` |
| 2026-07-16 | `MI-0013` | 启动任务；完成 API、SRS、SAD、DDD、权限、异常、缓存、测试和 RoadMap 基线核对，确定本项只返回 metrics-only 指标与质量摘要，不实现评分/地点名称解析/分析持久化 | 尚未运行本任务验证 | 实现 Application 查询用例、Web endpoint、ProblemDetails 映射和 API 集成测试；不处理 `MI-0002` 至 `MI-0004` |
| 2026-07-16 | `MI-0012` | 完成标准预报 L1 缓存边界；键包含环境、数据域、Provider/模型、网格坐标、UTC 小时范围和标准化版本；实现 IMemoryCache TTL、Stale 窗口、单航班刷新、缓存故障旁路和批次/点位/指标来源质量传递；同步部署与 RoadMap 文档 | `dotnet build` 0 错误；`dotnet test` 59/59 通过；`dotnet format --verify-no-changes`、`git diff --check`、28 个非 JSON/YAML 文本 BOM/CRLF 检查通过；有 NU1900、NU1903 警告；未执行真实 Redis/PostgreSQL 集成 | 下一项按用户指令选择阶段 1 查询/API 或 Dashboard；不处理 `MI-0002` 至 `MI-0004` |
| 2026-07-16 | `MI-0012` | 启动任务；完成缓存、Provider、日志、异常、开发规范、测试方案和 RoadMap 基线核对，登记 L1 缓存实现范围 | 尚未运行本任务验证 | 实现版本化 `ForecastCacheKey`、`IMemoryCache` L1、单航班回源和 Stale 质量语义；不处理 `MI-0002` 至 `MI-0004` |
| 2026-07-16 | `MI-0011` | 完成 ForecastBatch 仓储端口、EF 追加/读取和领域映射；支持 24/72/168 小时范围，保留空值、质量、缺失位图和指标来源；SQLite 对 `DateTimeOffset` 范围查询采用受限候选集本地判断 | `dotnet build` 0 错误；`dotnet test` 47/47 通过；`dotnet format --verify-no-changes`、`git diff --check`、11 个非 JSON/YAML 文本 BOM/CRLF 检查通过；有 NU1900、NU1903 警告；未连接真实 PostgreSQL | 等待用户指定下一项；不处理 `MI-0002` 至 `MI-0004` |
| 2026-07-16 | `MI-0011` | 启动任务；完成台账、工作区、数据库实体/映射/迁移和相关设计文档核对，确认当前无已有 ForecastBatch 仓储实现 | 尚未运行本任务验证 | 实现 Application 仓储端口、EF 仓储和领域映射，补充保存/读取测试；不处理 `MI-0002` 至 `MI-0004` |
| 2026-07-16 | `MI-0010` | 完成 ForecastSnapshot、ForecastSnapshotPoint、SnapshotQuality、SourceBatchReference 和 Application ForecastSnapshotAssembler；实现 UTC 时间并集、最近点上限、显式批次/指标 Provider 选择、来源追溯和质量传递，并注册 Web DI | `dotnet build` 0 错误；`dotnet test` 43/43 通过；`dotnet format --verify-no-changes`、`git diff --check`、13 个非 JSON/YAML 文本 BOM/CRLF 检查通过；有 NU1900、NU1903 警告 | 等待用户指定下一项；不处理 `MI-0002` 至 `MI-0004` |
| 2026-07-16 | `MI-0009` | 完成 Open-Meteo Weather/Marine Provider、DTO、防腐映射、质量校验、HTTP 异常映射、DI 注册和固定契约样本；保留 `ModelUnsupported` 质量标记并同步设计/部署文档 | `dotnet build` 0 错误；`dotnet test` 35/35 通过；真实 Weather/Marine 只读请求成功；`dotnet format --verify-no-changes`、`git diff --check`、16 个非 JSON/YAML 文本 BOM/CRLF 检查通过；有 NU1900、NU1903 警告 | 等待用户指定下一项；不处理 `MI-0002` 至 `MI-0004` |
| 2026-07-15 | `MI-0008` | 建立 `/health/live`、`/health/ready`、数据库有界连接检查、Serilog JSON 结构化日志、W3C Trace、OTLP 可选导出、UTC 时间字段和敏感信息脱敏，并同步日志/部署文档 | `dotnet build` 0 错误；`dotnet test` 23/23 通过；`dotnet format --verify-no-changes`、`git diff --check`、BOM/CRLF 检查通过；有 NU1900 审计源和 NU1903 SQLite 漏洞警告；未连接真实 PostgreSQL | 等待用户指定下一项；`MI-0002` 至 `MI-0004`由用户人工处理 |
| 2026-07-15 | `MI-0007` | 建立 PostgreSQL/SQLite 基础配置、EF Core 持久化映射、四张预报存储表、首个迁移，并接入 Web DI | `dotnet build` 0 错误；`dotnet test` 17/17 通过；`dotnet format --verify-no-changes`、`git diff --check`、BOM/CRLF 检查通过；有 NU1900、NU1903 警告；未执行真实 PostgreSQL 集成迁移；Web host 进程探针因 PowerShell `Start-Process` 重复 PATH 环境键未完成 | 等待用户指定下一项；`MI-0002` 至 `MI-0004`由用户人工处理 |
| 2026-07-15 | `MI-0006` | 定义标准 ForecastBatch/ForecastPoint/MetricSource/质量模型、Weather/Marine/Tide 端口和 Provider 错误层 | `dotnet build` 0 错误；`dotnet test` 14/14 通过；`dotnet format --verify-no-changes`、`git diff --check`、BOM/CRLF 检查通过；有 4 个 NU1900 审计源警告 | 等待用户指定下一项；`MI-0002` 至 `MI-0004`由用户人工处理 |
| 2026-07-15 | `MI-0005` | 建立 .NET 10 模块化单体工程骨架和 CI | `dotnet restore` 成功但有 8 个 NU1900 审计源警告；`dotnet build`、`dotnet test`（4/4）、`dotnet format --verify-no-changes`、`git diff --check`、BOM/CRLF 检查通过；Web 首页 HTTP 200 | 等待用户指定下一项；按 RoadMap 默认选择 `MI-0002` |
| 2026-07-13 | `MI-0001` | 建立统一 Agent 入口、文档路由、待办/已办台账及中断恢复协议 | 本地链接全部有效；`git diff --check`、UTF-8 BOM 与 CRLF 检查通过 | 按用户指令选择 `MI-0002` 或其他新任务 |

## 11. 台账维护检查

- [x] 是否只有一个 `IN_PROGRESS` 任务？
- [x] 当前状态、任务清单和实际 Git 差异是否一致？
- [x] 未完成任务是否写明了可执行的下一步？
- [x] 每个 `PAUSED` / `BLOCKED` 任务是否保留独立恢复详情？
- [x] 完成任务是否记录了验证结果与文档影响？
- [x] `CANCELLED` 任务是否已归档并记录取消依据？
- [x] 本次会话是否追加了会话记录？
- [x] 本次修改的文本文件是否符合 UTF-8 with BOM 与 CRLF？

以上是每次收尾的检查模板，不代表当前检查尚未执行；实际结果应写入“当前执行状态”和当次会话记录。

