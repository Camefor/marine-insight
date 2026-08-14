# Marine Insight

> 海岛海况智能决策平台 —— 为海钓、登岛、乘船、露营和摄影用户提供快速、可解释、可追溯的海况辅助决策。

在线体验：[https://marine.loyalme.life](https://marine.loyalme.life)

普通天气应用只回答“是否下雨、温度多少”，但海岛活动的风险由多项海洋气象指标共同决定：平均风速不高时远方天气系统仍可能形成大浪或长周期涌浪；阵风、雷暴、低能见度和潮位变化也会影响登岛与返航安全。Marine Insight 聚合标准化数据，执行确定性分析与活动评分，用直观页面给出风险原因、适宜时段和返航提醒。

## 功能特性

- **地点搜索与地图选点**：按名称搜索预置海岛/码头，或在地图（天地图）上点击选点、手动输入经纬度。
- **逐小时预报**：支持 24 / 72 / 168 小时逐小时海况查询。
- **海况风险分析**：由确定性规则引擎综合风、阵风、浪高、浪周期、涌浪、雷暴/CAPE、能见度等指标给出风险等级与风险贡献。
- **活动评分**：针对海钓、乘船、登岛、露营、摄影五类活动分别评分。
- **推荐时间窗与返航提醒**：给出适宜出行的连续时间窗，并标识风险上升点与保守返航截止。
- **趋势图与小时详情**：分数 / 风 / 浪三组趋势，点击任意小时查看完整指标、风险与来源。
- **AI 智能解读**：生成受规则约束的自然语言解读，外部 AI 不可用时自动降级为规则模板。
- **数据来源追溯**：每项结论均可追溯到原始指标、数据来源、抓取时间与算法版本。
- **用户工作区**：登录后可收藏地点、查看查询历史、设置单位与偏好。
- **缓存与降级**：外部数据源或 AI 故障时自动使用缓存或规则化降级结果。

## 技术栈

- **.NET 10** / ASP.NET Core / Blazor Web App（InteractiveServer）
- **领域驱动设计（DDD）** 分层：Domain / Application / Infrastructure / Web
- **EF Core** + PostgreSQL（生产）/ SQLite（本地开发）
- **ASP.NET Core Identity**（认证与用户工作区）
- **Docker + Caddy**（容器化与自动 HTTPS）
- 数据源：Open-Meteo（天气/海浪）、天地图（地图）、WorldTides（潮汐）、SiliconFlow（AI 解读）

## 快速开始

### 本地运行

```bash
dotnet run --project src/MarineInsight.Web
```

默认使用 SQLite 与开发配置。地图、AI、潮汐等外部能力需通过 .NET User Secrets 或环境变量注入 Key（不写入源码），详见 [`docs/18-部署文档.md`](docs/18-部署文档.md)。

### 测试

```bash
dotnet test MarineInsight.slnx
```

### Docker 部署

```bash
docker compose up -d --build
```

生产部署需配置数据库连接、天地图 Key、AI Key 等 Secret，完整流程见 [`docs/19-Docker部署.md`](docs/19-Docker部署.md) 与 [`docs/22-腾讯云生产部署手册.md`](docs/22-腾讯云生产部署手册.md)。

## 架构概览

```text
src/
├── MarineInsight.Domain/          # 领域模型、规则引擎、评分算法（不依赖基础设施）
├── MarineInsight.Application/     # 用例编排、端口抽象、查询/命令
├── MarineInsight.Infrastructure/  # EF Core、数据源适配器、缓存、外部服务
├── MarineInsight.Migrations.PostgreSql/ # PostgreSQL 迁移
└── MarineInsight.Web/             # Blazor Web App、API、页面与静态资源
```

安全结论由确定性规则引擎生成；AI 只负责解释，不参与评分、不覆盖硬性禁行规则。所有时间在内部以 UTC 统一存储与计算，展示层按客户端时区转换。

## 文档导航

完整需求与设计基线见 [`docs/README.md`](docs/README.md)，关键文档：

| 文档 | 内容 |
| --- | --- |
| [产品需求（PRD）](docs/01-产品需求(PRD).md) | 用户场景、功能优先级与产品验收 |
| [系统架构设计（SAD）](docs/03-系统架构设计(SAD).md) | 分层架构、技术选型与关键流程 |
| [海况分析引擎设计](docs/08-海况分析引擎设计.md) | 分析流程、风险规则与活动适配 |
| [评分算法设计](docs/09-评分算法设计.md) | 指标阈值、权重、硬性禁行与校准 |
| [数据库设计](docs/05-数据库设计.md) | 数据模型、表结构、索引与保留策略 |
| [API 接口设计](docs/06-API接口设计.md) | API 规范、端点、响应与错误码 |
| [开发 RoadMap](docs/21-开发RoadMap.md) | 实施阶段、交付物与版本计划 |

## 免责声明

本系统提供辅助决策，**不替代**官方气象、海事、港口或船方安全决定，不对人身和财产安全作保证。任何高风险提示、官方预警或现场管控均优先于系统评分。
