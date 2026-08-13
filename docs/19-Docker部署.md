# Docker 部署

## 1. 目标

使用不可变、多阶段、非 root 镜像交付 Marine Insight，并通过 Docker Compose 支持单机开发、测试和小规模生产。大规模部署可迁移到编排平台，但服务边界和环境变量保持一致。

## 2. 前置条件

| 依赖 | 版本建议 | 说明 |
| --- | --- | --- |
| Docker Engine | 26+ | Linux 容器 |
| Docker Compose | v2.27+ | 使用 `docker compose` |
| 主机 | 2 CPU / 4 GB 起 | 需根据数据库和监控调整 |
| 域名/TLS | 生产必需 | 由反向代理或平台终止 TLS |

## 3. 镜像设计

| 镜像 | 基础镜像 | 端口 | 用户 |
| --- | --- | --- | --- |
| `marine-insight-web` | `mcr.microsoft.com/dotnet/aspnet:10.0` | 8080 | 非 root |
| `marine-insight-worker` | `mcr.microsoft.com/dotnet/runtime:10.0` | 无 | 非 root |

构建阶段使用对应 `sdk:10.0`，先复制项目文件并还原，再复制源码发布，提高层缓存命中。固定到受控补丁版本或镜像摘要，并由依赖更新流程定期升级。

## 4. Dockerfile 约定

- 使用多阶段构建和 `dotnet publish --no-restore`。
- 运行阶段不包含 SDK、源码、测试资产和本地 Secret。
- 设置 `ASPNETCORE_HTTP_PORTS=8080`，由反向代理提供 HTTPS。
- 使用非 root 用户，只授予应用目录必要权限。
- 添加 OCI Label：版本、提交 SHA、仓库和构建时间。
- 健康检查请求 `/health/live`，不把外部 AI 故障当作容器死亡。
- `.dockerignore` 排除 `.git`、`bin`、`obj`、测试输出、Secret 和本地数据库。

## 5. Compose 服务

| 服务 | 职责 | 依赖 | 持久化 |
| --- | --- | --- | --- |
| `web` | Blazor、API、认证 | `postgres`，可选 `redis` | 不使用本地业务卷 |
| `worker` | 预热、清理、通知 | `postgres`、`redis` | 无 |
| `postgres` | 主数据库 | 无 | 命名卷 `postgres-data` |
| `redis` | 共享缓存/锁 | 无 | 可不持久化；按策略配置 |
| `reverse-proxy` | TLS、压缩、访问入口 | `web` | 证书/配置 |
| `otel-collector` | 观测转发（可选） | 无 | 配置卷 |

应用只等待依赖健康，不使用固定 sleep。数据库迁移作为一次性 `migrate` 服务或发布任务执行，不由多个 Web 容器竞争执行。

## 6. 网络与端口

- 仅反向代理暴露 80/443；Web 8080、PostgreSQL 5432、Redis 6379 只在内部网络可见。
- 将数据库/缓存网络与入口网络分离，Web 同时加入二者。
- 生产禁止将 PostgreSQL 和 Redis 绑定到 `0.0.0.0`。
- 配置可信代理和 `ForwardedHeaders`，防止伪造客户端 IP 破坏限流。

## 7. 环境变量

| 变量 | 必填 | 说明 |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | 是 | `Production` 等环境 |
| `ConnectionStrings__MarineInsight` | 是 | PostgreSQL 连接 |
| `Redis__Enabled` | 否 | 是否启用 Redis |
| `Redis__ConnectionString` | 条件 | Redis 连接 |
| `Caching__Forecast__Environment` | 否 | 缓存环境隔离段，生产应显式设置 |
| `Caching__Forecast__NormalizerVersion` | 否 | 标准化语义版本，改变映射后递增 |
| `Caching__Forecast__CoordinatePrecision` | 否 | 缓存坐标精度，默认 4 位小数 |
| `Caching__Forecast__FreshLifetime` | 否 | L1 新鲜 TTL，默认 15 分钟 |
| `Caching__Forecast__StaleIfErrorLifetime` | 否 | Provider 失败时的旧值降级窗口，默认 2 小时 |
| `ForecastProviders__OpenMeteo__Enabled` | 否 | Open-Meteo 主源开关 |
| `ForecastProviders__OpenMeteo__WeatherBaseUrl` | 否 | Weather API 地址 |
| `ForecastProviders__OpenMeteo__MarineBaseUrl` | 否 | Marine API 地址 |
| `ForecastProviders__OpenMeteo__WeatherModel` | 否 | Weather 请求模型，默认 `best_match` |
| `ForecastProviders__OpenMeteo__MarineModel` | 否 | Marine 请求模型，默认 `best_match` |
| `ForecastProviders__OpenMeteo__Timeout` | 否 | Provider 请求超时，默认 15 秒 |
| `ForecastProviders__OpenMeteo__ApiKey` | 条件 | 商业套餐使用时注入 |
| `ForecastProviders__Stormglass__Enabled` | 否 | 专业增强开关，默认关闭 |
| `ForecastProviders__Stormglass__ApiKey` | 条件 | 启用时注入 |
| `TideProviders__WorldTides__Enabled` | 否 | 潮汐开关 |
| `TideProviders__WorldTides__ApiKey` | 条件 | 启用时注入 |
| `AI__Enabled` | 否 | AI 开关 |
| `AI__ApiKey` | 条件 | Secret 注入 |
| `OpenTelemetry__Endpoint` | 否 | OTel Collector 地址 |

非敏感默认值可放 Compose 配置；Secret 使用 Docker Secret、宿主机受限文件或外部 Secret 管理器，不提交 `.env` 生产文件。

## 8. 构建与启动

```powershell
docker compose build --pull
docker compose run --rm migrate
docker compose up -d
docker compose ps
```

启动后先检查 `web` 和数据库健康，再执行部署文档中的冒烟测试。镜像发布到 Registry 时使用 `v1.0.0` 和 Git SHA 标签，不复用可变 `latest` 作为回滚依据。

## 9. 健康检查与依赖

- PostgreSQL 使用 `pg_isready`。
- Redis 使用 `redis-cli ping`，认证参数通过 Secret 提供。
- Web 使用 `/health/live`；反向代理流量切换使用 `/health/ready`。
- `/health/live` 不访问数据库或外部 Provider；`/health/ready` 只验证基础数据库连接并使用有限超时。
- Worker 提供心跳指标或独立健康端点，长任务记录最后成功时间。

Compose 的 `depends_on` 只表达启动依赖，不能替代应用内重试和韧性策略。

当前 `compose.yaml` 使用 PostgreSQL 17、一次性 `migrate`、非 root Web 和 Caddy 四服务拓扑。数据库仅加入内部 `data` 网络，Web 同时加入 `data`/`ingress`，Caddy 固定为 `172.30.0.10` 并作为唯一公开入口。连接字符串通过名为 `ConnectionStrings__MarineInsight` 的 Key-per-file Secret 注入；示例 Secret 只用于一次性本地环境。

## 10. 数据持久化与备份

- PostgreSQL 使用命名卷或绑定到已验证的数据盘，禁止把数据库写入容器层。
- 备份输出到独立于数据库卷的位置并加密，定期复制到异机/对象存储。
- 升级 PostgreSQL 主版本前执行逻辑备份和恢复演练。
- Redis 默认作为可丢失缓存；若开启持久化，也不能替代 PostgreSQL 备份。
- `scripts/backup-postgres.ps1` 将自定义格式逻辑备份写入宿主 `backups/`；`scripts/restore-postgres.ps1` 只接受该目录内文件并要求显式确认。备份目录必须由部署方加密、异机复制和配置保留策略。

## 11. 资源与安全

- 为 Web、Worker、PostgreSQL 和 Redis 设置内存/CPU 限制与合理预留。
- 根文件系统尽可能只读，仅挂载必要临时目录。
- 删除不需要的 Linux Capabilities，启用 `no-new-privileges`。
- 定期扫描基础镜像和 NuGet 依赖；高危漏洞在发布前处理或记录风险接受。
- 容器日志配置大小和文件数量限制，避免占满磁盘。

## 12. 升级与回滚

1. 拉取并验证新镜像摘要。
2. 备份数据库并运行一次性迁移。
3. 更新一个实例或在 Staging 验证后切换生产。
4. 观察健康、5xx、数据质量和 Provider 指标。
5. 失败时切回上一镜像；算法问题优先回滚算法版本。

不得在没有备份和兼容评估时执行数据库降级命令。

## 13. 故障排查

| 现象 | 检查 | 处理 |
| --- | --- | --- |
| Web 不就绪 | `docker compose ps/logs`、数据库和配置健康 | 修复 Secret/迁移/连接，不反复盲目重启 |
| 查询持续 503 | Open-Meteo Weather/Marine、缓存和网络 | 分域检查端点，使用有效缓存；不得自动无限调用 Stormglass |
| 潮汐为空 | WorldTides 开关、Credit、缓存和目标坐标 | 基础海况继续运行，补充额度或修复配置 |
| Redis 错误 | Redis 健康和连接数 | 允许 Web 绕过缓存，修复后观察命中率 |
| 数据库磁盘增长 | 表保留、日志、备份位置 | 执行归档清理和容量扩展 |
| SignalR 断线 | 代理 WebSocket、超时和资源 | 修正代理并检查 Web 负载 |
| 时间显示错误 | 容器 UTC、地点时区和 Provider 时间映射 | 修复标准化，不修改宿主时区规避 |

## 14. 变更记录

| 版本 | 日期 | 变更说明 |
| --- | --- | --- |
| 1.0 | 2026-07-13 | 定义非 root 镜像、Compose 拓扑、Secret 和回滚规范 |
| 1.1 | 2026-07-13 | 升级 .NET 10 镜像并替换数据源环境变量 |
| 1.2 | 2026-07-15 | 补充 Web 存活/就绪探针的容器使用约定 |
| 1.3 | 2026-08-13 | 落地 Dockerfile、Compose、Caddy、Key-per-file Secret 和备份恢复脚本 |
