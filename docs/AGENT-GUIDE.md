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
| 当前任务 ID | `MI-0014` |
| 当前状态 | `DONE` |
| 当前目标 | 建立只读地点目录查询边界，支持预置地点搜索、附近地点查询，并为 `POST /api/v1/marine-analyses` 接入 `locationId`；`MI-0002`、`MI-0003`、`MI-0004`由用户人工处理并保持 TODO |
| 最后完成动作 | 完成 `MI-0014`：实现 Location 领域模型、只读 EF 仓储、预置地点迁移、地点搜索/附近 API、分析 `locationId` 解析及地点元数据投影；恢复后已在 Development Web Host 上应用 SQLite 迁移并完成真实 HTTP 验证；同步 API、DDD、数据库、异常、测试和 RoadMap 文档 |
| 下一步动作 | 等待用户指定下一项；不接管 `MI-0002`、`MI-0003`、`MI-0004` |
| 涉及文件 | `src/MarineInsight.Domain/Location/`、`src/MarineInsight.Application/Locations/`、`src/MarineInsight.Infrastructure/Persistence/`、`src/MarineInsight.Web/Api/`、相关测试和地点/API/数据库/DDD/测试/RoadMap 文档 |
| 验证结果 | `dotnet build MarineInsight.slnx --no-restore --configuration Release` 0 错误；`dotnet test MarineInsight.slnx --no-restore --configuration Release` 75/75 通过；地点 API 定向测试 3/3 通过；Development Web Host 的 `/health/live`、地点搜索和附近查询真实 HTTP 验证通过；SQLite 已应用两条迁移；`dotnet format --verify-no-changes`、`git diff --check`、本次 31 个非 JSON/YAML 文本 BOM/CRLF 检查通过；仍有 NU1900、NU1903 警告；真实 PostgreSQL/Redis 未连接 |
| 阻塞/待确认 | 无代码阻塞；地点查询只使用已有 `locations` 表，不接入外部地理编码；本次不处理用户自行人工验证的 `MI-0002`、`MI-0003`、`MI-0004` |
| 最后更新 | 2026-07-16 |

<!-- agent-state:end -->

## 8. 未完成任务

### 8.1 待办任务

| 完成 | ID | 优先级 | 状态 | 任务 | 来源与验收 |
| --- | --- | --- | --- | --- | --- |

当前没有待办任务；用户自行处理的 `MI-0002`、`MI-0003`、`MI-0004` 不纳入本 Agent 实施范围。

### 8.2 暂停/阻塞任务恢复详情

当前没有 `PAUSED` 或 `BLOCKED` 任务。

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

### 9.2 取消任务

当前没有 `CANCELLED` 任务。取消任务必须保留任务 ID、取消日期、原任务目标、取消原因和用户/决策来源。

## 10. 会话记录

按时间倒序追加，保留最近 30 条；清理旧记录时不得删除待办、已办和关键决策。

| 日期 | 任务 ID | 会话结果 | 验证 | 下一步 |
| --- | --- | --- | --- | --- |
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

