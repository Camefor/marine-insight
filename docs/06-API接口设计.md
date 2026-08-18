# API 接口设计

## 1. 设计原则

- 对外暴露业务资源，不暴露 Open-Meteo、Stormglass 等 Provider 的原始 DTO。
- API 版本、算法版本和数据来源分别表达，避免概念混用。
- 查询结果可缓存、可追踪；创建收藏等写操作具备幂等与授权校验。
- 错误采用 RFC 9457 `ProblemDetails`，业务错误码保持稳定。

## 2. 基础约定

| 项目 | 约定 |
| --- | --- |
| Base URL | `/api/v1` |
| 数据格式 | JSON，属性使用 camelCase |
| 时间格式 | ISO 8601，包含 UTC 偏移 |
| 内部单位 | m/s、m、s、degree、C、hPa |
| 分页 | `page`, `pageSize`，最大 100 |
| 追踪 | 响应头和错误体返回 `traceId` |
| 幂等 | 写接口可接受 `Idempotency-Key` |

## 3. 认证与授权

- 查询类 P0 API 允许匿名访问，但执行 IP/设备维度限流。
- 收藏、历史和设置使用 Cookie 或 Bearer Token 认证。
- 管理 API 要求 `Administrator` 角色和二次审计。
- 前端隐藏按钮不构成授权，所有权限由 API 再次校验。

## 4. 接口清单

| 模块 | 方法 | 路径 | 说明 | 权限 |
| --- | --- | --- | --- | --- |
| 地点 | GET | `/locations/search?q={text}` | 搜索地点 | 匿名 |
| 地点 | GET | `/locations/nearby?lat=&lon=` | 查询附近预置地点 | 匿名 |
| 分析 | POST | `/marine-analyses` | 查询并生成海况分析 | 匿名 |
| 分析 | GET | `/marine-analyses/{id}` | 获取已持久化分析报告（属主） | 登录 |
| 预报 | GET | `/forecast-batches/{id}/points` | 获取逐小时标准预报 | 匿名 |
| 收藏 | GET | `/favorites` | 收藏列表 | 登录 |
| 收藏 | POST | `/favorites` | 新增收藏 | 登录 |
| 收藏 | PUT | `/favorites/{id}` | 修改默认活动/备注/排序 | 登录 |
| 收藏 | DELETE | `/favorites/{id}` | 删除收藏 | 登录 |
| 历史 | GET | `/query-history` | 查询历史 | 登录 |
| 历史 | DELETE | `/query-history/{id}` | 删除单条查询历史 | 登录 |
| 历史 | DELETE | `/query-history` | 清空查询历史 | 登录 |
| 地点 | GET | `/user-locations` | 我的地点列表 | 登录 |
| 地点 | POST | `/user-locations` | 新增自定义预设地点 | 登录 |
| 地点 | PUT | `/user-locations/{id}` | 修改自定义预设地点 | 登录 |
| 地点 | DELETE | `/user-locations/{id}` | 删除自定义预设地点 | 登录 |
| 设置 | GET/PUT | `/user-settings` | 用户单位和偏好 | 登录 |
| 管理 | GET | `/admin/providers` | Provider 状态与配额摘要 | 管理员 |
| 管理 | POST | `/admin/algorithms/{id}/publish` | 发布算法版本 | 管理员 |
| 管理 | GET | `/admin/locations` | 预置地点列表 | 管理员 |
| 管理 | POST | `/admin/locations` | 新增预置地点 | 管理员 |
| 管理 | PUT | `/admin/locations/{id}` | 修改预置地点 | 管理员 |
| 管理 | DELETE | `/admin/locations/{id}` | 删除预置地点（返回级联收藏引用数） | 管理员 |
| 管理 | GET | `/admin/users` | 已注册用户只读列表 | 管理员 |
| 系统 | GET | `/health/live` | 存活检查 | 基础设施 |
| 系统 | GET | `/health/ready` | 就绪检查 | 基础设施 |

## 5. 创建海况分析

### 5.1 请求

`POST /api/v1/marine-analyses`

```json
{
  "location": {
    "locationId": "8a477d67-73fa-4f43-b954-cd29d238a89d"
  },
  "from": "2026-07-15T08:00:00+08:00",
  "hours": 72,
  "activities": ["shoreFishing", "boat", "camping"],
  "units": {
    "windSpeed": "mps",
    "waveHeight": "meter",
    "temperature": "celsius"
  }
}
```

也允许使用坐标：

```json
{
  "location": { "latitude": 30.194, "longitude": 122.687 },
  "from": "2026-07-15T08:00:00+08:00",
  "hours": 24,
  "activities": ["photography"]
}
```

约束：`hours` 仅允许 `24`、`72`、`168`；经纬度必须成对出现；活动类型必须来自服务端枚举。

当前 `MI-0014` 已支持预置地点的 `locationId` 输入：API 先从只读地点目录解析坐标、展示名称和 IANA 时区，再执行分析查询；未知地点返回 `404 LOCATION_NOT_FOUND`。`locationId` 与坐标不能同时提供。`MI-0017` 已校验 `activities` 并按请求活动返回活动分数；`MI-0018` 已返回 `recommendedWindows`。`units` 仍仅保留为协议字段，暂不做展示单位换算。

### 5.2 响应

```json
{
  "analysisId": "82f9ff76-bb11-4456-93e5-78d669e609ea",
  "sourceBatchIds": [
    "35d4af68-6297-46d2-bc7d-fc0d62bb79bd",
    "bea327c7-6039-43b9-81f1-c1559a0c4987"
  ],
  "location": {
    "locationId": "8a477d67-73fa-4f43-b954-cd29d238a89d",
    "displayName": "东极岛",
    "latitude": 30.194,
    "longitude": 122.687,
    "timeZone": "Asia/Shanghai"
  },
  "sources": [
    {
      "batchId": "35d4af68-6297-46d2-bc7d-fc0d62bb79bd",
      "dataDomain": "weather",
      "provider": "open-meteo",
      "model": "best-match",
      "issuedAt": "2026-07-14T20:00:00Z",
      "fetchedAt": "2026-07-15T00:01:18Z",
      "cacheStatus": "hit",
      "quality": "valid"
    },
    {
      "batchId": "bea327c7-6039-43b9-81f1-c1559a0c4987",
      "dataDomain": "marine",
      "provider": "open-meteo",
      "model": "best-match",
      "issuedAt": "2026-07-14T18:00:00Z",
      "fetchedAt": "2026-07-15T00:01:19Z",
      "cacheStatus": "miss",
      "quality": "valid"
    }
  ],
  "overall": {
    "score": 82,
    "riskLevel": "good",
    "confidence": 0.91,
    "algorithmVersion": "marine-score-1.0.0"
  },
  "activities": [
    { "type": "shoreFishing", "score": 76, "riskLevel": "moderate" },
    { "type": "boat", "score": 84, "riskLevel": "good" }
  ],
  "risks": [
    {
      "code": "SWELL_LONG_PERIOD_SHORE",
      "severity": "warning",
      "forecastTime": "2026-07-15T14:00:00+08:00",
      "message": "长周期涌浪会增加礁石拍浪风险",
      "actual": { "swellHeightM": 0.9, "swellPeriodS": 12 }
    }
  ],
  "recommendedWindows": [
    {
      "activity": "shoreFishing",
      "start": "2026-07-15T08:00:00+08:00",
      "end": "2026-07-15T15:00:00+08:00",
      "returnBefore": "2026-07-15T14:30:00+08:00"
    }
  ],
  "hourly": [],
  "disclaimer": "结果仅供辅助决策，请以官方预警和现场管理为准。",
  "traceId": "00-..."
}
```

`hourly` 可按产品性能策略返回组装后的摘要。完整原始标准点位通过 `sourceBatchIds` 对应的 `/forecast-batches/{id}/points` 分别获取，每个指标仍携带来源引用。

### 5.3 当前分析响应

`MI-0018` 已在 `POST /api/v1/marine-analyses` 返回确定性分析投影。当前响应包含 `overall`、`activities`、`recommendedWindows`、`risks` 和逐小时 `hourly[].overall/hourly[].activities/hourly[].risks`。

### 5.4 获取持久化分析报告

`GET /api/v1/marine-analyses/{id}`（`MI-0029`，需登录）返回登录用户在 Dashboard 查询时已落库的分析结果摘要。匿名请求重定向至登录页；非属主或不存在的 `id` 返回 `404 ANALYSIS_NOT_FOUND`。

```json
{
  "id": "82f9ff76-bb11-4456-93e5-78d669e609ea",
  "location": { "locationId": "8a477d67-73fa-4f43-b954-cd29d238a89d" },
  "range": { "startUtc": "2026-07-16T00:00:00Z", "endUtc": "2026-07-17T00:00:00Z", "hours": 24 },
  "algorithmVersion": "marine-score-1.0.0",
  "overall": { "score": 72, "riskLevel": "caution", "confidence": 0.8 },
  "risks": [
    { "forecastTimeUtc": "2026-07-16T02:00:00Z", "ruleCode": "swell-high", "severity": "warning", "actual": 2.5, "threshold": 2.0, "penalty": 15, "message": "长周期涌浪偏高" }
  ],
  "sources": [
    { "batchId": "…", "dataDomain": "weather", "providerCode": "open-meteo", "sourceModel": "weather-v1", "sourceRole": "primary", "selectionPolicy": "forecast-snapshot-assembler.v1" }
  ],
  "recommendedWindow": { "startUtc": "2026-07-16T03:00:00Z", "endUtc": "2026-07-16T08:00:00Z", "returnBeforeUtc": "2026-07-16T07:00:00Z" },
  "createdAtUtc": "2026-07-16T12:00:00Z"
}
```

```json
{
  "analysisStatus": "analyzed",
  "analysisId": "82f9ff76-bb11-4456-93e5-78d669e609ea",
  "algorithmVersion": "marine-score-1.0.0",
  "cache": {
    "key": "mi:analysis:v1:forecast-snapshot-assembler.v1:8f4...:marine-score-1.0.0:boat",
    "eTag": "\"4f2d8f53e5d64d0e8ad07f179067a4a5\"",
    "sourceBatchSetHash": "8f4...",
    "sourceSelectionPolicy": "forecast-snapshot-assembler.v1",
    "algorithmVersion": "marine-score-1.0.0",
    "activities": ["boat"]
  },
  "location": { "latitude": 30.194, "longitude": 122.687 },
  "range": {
    "from": "2026-07-15T00:00:00Z",
    "to": "2026-07-18T00:00:00Z",
    "hours": 72
  },
  "sources": [
    {
      "batchId": "35d4af68-6297-46d2-bc7d-fc0d62bb79bd",
      "dataDomain": "weather",
      "provider": "open-meteo",
      "model": "best-match",
      "issuedAt": "2026-07-14T20:00:00Z",
      "fetchedAt": "2026-07-15T00:01:18Z",
      "cacheStatus": "miss",
      "quality": { "status": "valid", "freshness": "fresh" }
    }
  ],
  "quality": {
    "status": "partial",
    "freshness": "fresh",
    "completeness": 0.95,
    "flags": [],
    "missingMetrics": [],
    "missingDomains": []
  },
  "overall": {
    "score": 85,
    "riskLevel": "good",
    "confidence": 1,
    "algorithmVersion": "marine-score-1.0.0"
  },
  "activities": [
    { "type": "boat", "score": 81, "riskLevel": "good", "confidence": 1, "algorithmVersion": "marine-score-1.0.0" }
  ],
  "recommendedWindows": [
    {
      "activity": "boat",
      "start": "2026-07-15T00:00:00Z",
      "end": "2026-07-15T04:00:00Z",
      "returnBefore": "2026-07-15T04:00:00Z",
      "riskRisesAt": "2026-07-15T05:00:00Z",
      "riskReason": "活动评分在短时间内明显下降。",
      "bestScore": 86,
      "minimumScore": 75,
      "durationHours": 4
    }
  ],
  "risks": [
    {
      "code": "WAVE_HEIGHT_BASE",
      "kind": "basePenalty",
      "severity": "info",
      "forecastTime": "2026-07-15T00:00:00Z",
      "metric": "waveHeightM",
      "actual": 0.8,
      "threshold": null,
      "penalty": 12,
      "message": "基础指标惩罚。"
    }
  ],
  "hourly": [
    {
      "forecastTime": "2026-07-15T00:00:00Z",
      "metrics": { "windSpeedMs": 4.2, "waveHeightM": 0.6 },
      "quality": { "status": "valid", "freshness": "fresh" },
      "sources": [],
      "overall": { "score": 85, "riskLevel": "good", "confidence": 1, "algorithmVersion": "marine-score-1.0.0" },
      "activities": [],
      "risks": []
    }
  ],
  "explanation": {
    "source": "template",
    "degraded": false,
    "headline": "整体海况良好，适宜乘船活动。",
    "summary": "风浪较小，适合安排乘船活动。",
    "activityNotes": [
      { "activity": "boat", "text": "可以安排乘船活动。" }
    ],
    "riskWindowText": null,
    "uncertaintyText": null,
    "disclaimer": "结果仅供辅助决策，请以官方预警和现场管理为准。",
    "promptVersion": "v1",
    "modelVersion": null,
    "locale": "zh-CN"
  },
  "disclaimer": "结果仅供辅助决策，请以官方预警和现场管理为准。",
  "traceId": "00-..."
}
```

`MI-0022` 后，响应头返回与 `cache.eTag` 一致的 `ETag`；客户端带相同 `If-None-Match` 时返回 `304 Not Modified`，不重复传输响应体。`cache.key` 和 `cache.eTag` 均由来源批次集合、来源选择策略、算法版本和归一化活动集合决定。`sources[].cacheStatus` 为 `hit`、`miss` 或 `stale`；当 Provider 在 Stale 窗口内失败时，响应保留旧批次并通过 `quality.freshness` 与质量 flags 表达降级。`activities` 为空或缺省时服务端默认返回五类活动分；传入未知活动返回 `400 VALIDATION_FAILED`。

`MI-0028` 后，响应新增非空 `explanation` 对象：`source` 为 `template`（规则模板）或 `ai`（模型生成）；`degraded=true` 表示 AI 关闭、调用失败或校验不通过时已降级为模板，但 HTTP 状态仍为 `200`，不因可选 AI 失败阻断分析。`explanation` 字段位于 `hourly` 之后、`disclaimer` 之前，作为纯增量不升级 API 主版本。

## 6. 地点目录查询

`GET /api/v1/locations/search?q=东极岛&limit=10`

响应为地点数组，字段包括 `id`、`displayName`、`locationType`、`latitude`、`longitude`、`timeZone` 和 `source`。当前只查询系统预置目录，不调用外部地理编码服务；模糊候选不得自动替用户选择，重名地点必须显示行政区域或坐标。

`GET /api/v1/locations/nearby?lat=30.194&lon=122.687&radiusKm=50&limit=10`

附近查询要求 `lat`、`lon` 成对出现，按球面距离升序返回预置地点。`radiusKm` 默认 50，允许范围为 `(0, 500]`；`limit` 默认 10，允许范围为 `[1, 50]`。搜索文本 `q` 必填，最多 160 个字符。

## 7. 收藏接口

新增收藏请求：

```json
{
  "locationId": "8a477d67-73fa-4f43-b954-cd29d238a89d",
  "defaultActivity": "shoreFishing",
  "note": "夏季常用钓点"
}
```

同一用户收藏同一地点返回 `409 FAVORITE_ALREADY_EXISTS`。删除必须校验资源所有者。

## 8. 后台管理接口

管理端点统一挂 `/api/v1/admin/**`，要求 `Administrator` 角色，写操作携带防伪头 `RequestVerificationToken`，并受 `admin` 限流策略（5/min/用户）约束。

### 8.1 预置地点管理

`POST/PUT /api/v1/admin/locations[/{id}]` 请求体：

```json
{
  "displayName": "枸杞岛",
  "latitude": 30.72,
  "longitude": 122.77,
  "timeZoneId": "Asia/Shanghai",
  "locationType": "island",
  "coastOrientationDeg": 45
}
```

- `POST` 返回 `201` 与新建地点；`PUT /{id}` 返回 `200`，地点不存在返回 `404 LOCATION_NOT_FOUND`。
- 名称 + 经纬度与既有预置地点冲突返回 `409 LOCATION_CONFLICT`。
- `DELETE /{id}` 返回 `200` 与 `{ "deleted": true, "cascadedFavoriteCount": n }`，`n` 为级联删除的收藏行数；被 `forecast_batches` 引用的地点返回 `409 LOCATION_IN_USE` 不可删除。
- 全部写操作落 `audit_logs`（`location.created/updated/deleted`），删除前由服务层预检外键约束。

### 8.2 用户列表

`GET /api/v1/admin/users` 返回只读用户摘要数组（`email`/`emailConfirmed`/`lockoutEnabled`/`lockoutEnd`/`accessFailedCount`），不暴露密码哈希。

## 9. 错误响应

```json
{
  "type": "https://marine-insight.local/problems/provider-unavailable",
  "title": "天气数据源暂时不可用",
  "status": 503,
  "detail": "当前没有可用的实时数据或有效缓存，请稍后重试。",
  "code": "PROVIDER_UNAVAILABLE",
  "traceId": "00-..."
}
```

| 错误码 | HTTP | 含义 |
| --- | --- | --- |
| `VALIDATION_FAILED` | 400 | 参数或业务输入无效 |
| `LOCATION_NOT_FOUND` | 404 | 地点不存在 |
| `ANALYSIS_NOT_FOUND` | 404 | 分析结果不存在或不可访问 |
| `FORECAST_INSUFFICIENT` | 422 | 关键字段不足，无法可靠分析 |
| `FAVORITE_ALREADY_EXISTS` | 409 | 重复收藏 |
| `LOCATION_CONFLICT` | 409 | 预置地点名称 + 经纬度重复 |
| `LOCATION_IN_USE` | 409 | 预置地点已被预报批次引用，禁止删除 |
| `RATE_LIMITED` | 429 | 请求超过配额 |
| `PROVIDER_UNAVAILABLE` | 503 | 外部数据源且缓存均不可用 |
| `AI_EXPLANATION_UNAVAILABLE` | 200/降级标记 | AI 失败但规则结果仍可返回 |

## 10. 缓存与条件请求

- 地点搜索可缓存 24 小时；预报按 Provider 更新节奏缓存 10-30 分钟。
- 分析响应的 ETag 由来源批次集合哈希、来源选择策略、算法版本和归一化活动集合组成；单位偏好仅影响展示换算，不进入领域分析缓存键。
- 客户端可发送 `If-None-Match`；服务端返回 `304` 时不重复传输结果。
- 任何缓存响应都必须保留原数据 `fetchedAt`，不能将命中时间伪装为数据时间。

## 11. 限流

| 场景 | 初始策略 |
| --- | --- |
| 匿名分析 | 每 IP 每分钟 10 次、每日软配额 |
| 登录分析 | 每用户每分钟 30 次 |
| 地点搜索 | 每 IP 每分钟 60 次 |
| 管理发布 | 每用户每分钟 5 次并审计 |

`MI-0026` 的 Blazor 账户表单使用静态 SSR POST：`POST /account/register`、`POST /account/login` 和 `POST /account/logout`。三者必须携带防伪令牌；账户组按来源 IP 每分钟最多 10 次。成功注册/登录只允许重定向到本站绝对路径，退出要求已认证；认证失败返回通用页面状态，不通过响应区分“邮箱不存在”和“密码错误”。这些端点服务浏览器 Cookie 会话，不作为外部 JSON API 契约。

`MI-0036` 已落地全接口限流与验证码：注册/登录表单需通过自研 SVG 验证码（服务端一次性 SHA-256、3 分钟过期），验证码失败返回 `?error=captcha` 且不触发锁定计数；`AddRateLimiter` 提供 `account`（10/min/IP）、`location`（60/min/IP）、`analysis`（匿名 10/IP、登录 30/用户，令牌桶）、`authenticated`（30/min/用户，工作区与分析报告）、`admin`（5/min/用户）五策略，`UseRateLimiter` 移到授权之后以便按 `context.User` 分桶；新增 `GET/POST /api/v1/user-locations`、`PUT/DELETE /api/v1/user-locations/{id}` 与 `DELETE /api/v1/query-history[/{id}]`（写操作均带防伪头 `RequestVerificationToken`）。

服务端根据 Provider 配额动态收紧回源，不影响缓存命中读取。

`MI-0027` 已实现登录用户的收藏、查询历史和单位设置端点，以及管理员只读运行状态端点。分析响应新增 `tide` 状态：`disabled` 表示未启用，`available` 表示潮汐可用，`degraded` 表示仍返回潮汐但 Credit 已低于告警阈值，`unavailable` 表示本次潮汐失败；可选潮汐失败不改变基础天气/海况分析的成功状态。

## 12. 版本与废弃

- URL 主版本用于破坏性协议变化，例如 `/api/v2`。
- 新增可选字段不升级主版本；删除或改变语义需要新版本。
- 废弃接口至少保留一个发布周期，并返回 `Deprecation` 与 `Sunset` 响应头。
- 算法版本通过响应字段独立管理，不等同于 API 版本。

## 13. 变更记录

| 版本 | 日期 | 变更说明 |
| --- | --- | --- |
| 1.0 | 2026-07-13 | 定义版本化业务 API、ProblemDetails 和分析响应 |
| 1.1 | 2026-07-13 | 分析响应改为返回 Open-Meteo 等多个来源批次及数据域 |
| 1.2 | 2026-07-16 | 增加 `POST /api/v1/marine-analyses` metrics-only 查询骨架、质量/来源/缓存投影和坐标校验契约 |
| 1.3 | 2026-07-30 | 增加 `MI-0017` 分析响应 `overall`、`activities`、`risks`、逐小时评估投影和活动参数校验 |
| 1.4 | 2026-07-31 | 增加 `MI-0018` 分析响应 `recommendedWindows`、风险上升点和返航截止投影 |
| 1.5 | 2026-07-31 | 增加 `MI-0022` 根级 `algorithmVersion`、`cache` 对象、`ETag` 响应头和 `If-None-Match` 条件请求 |
| 1.6 | 2026-08-12 | 记录 `MI-0026` 静态 SSR 账户表单端点、防伪、限流和本地重定向约束 |
| 1.7 | 2026-08-13 | 记录用户工作区、管理员运行状态和可降级 WorldTides 状态契约 |
| 1.8 | 2026-08-13 | 增加 `MI-0029` `GET /api/v1/marine-analyses/{id}` 持久化分析报告读取端点与 `ANALYSIS_NOT_FOUND` 错误码 |
| 1.8 | 2026-08-13 | 增加分析响应 `explanation` 字段与 AI/模板降级标记（MI-0028） |
| 1.9 | 2026-08-14 | 增加 `MI-0036` 用户地点 CRUD 与查询历史删除/清空端点，落地全接口限流策略与注册/登录验证码 |
| 2.0 | 2026-08-17 | 收藏请求体支持地图坐标点（`MI-0038`）：`POST/PUT /api/v1/favorites` 的 `locationId` 改为可空，新增 `displayName`/`latitude`/`longitude` 字段；`locationId` 非空为预置地点收藏，为空为地图坐标点收藏（冗余名称/坐标，服务端校验纬度 `[-90,90]`、经度 `[-180,180]`、名称 ≤200，按 `(用户, 坐标)` 去重） |
| 2.1 | 2026-08-18 | 增加 `MI-0039` 后台管理端点：`GET/POST/PUT/DELETE /api/v1/admin/locations[/{id}]` 与 `GET /api/v1/admin/users`（均要求 `Administrator` 角色 + `admin` 限流，写操作带防伪头），新增 `LOCATION_CONFLICT`/`LOCATION_IN_USE` 错误码，删除返回级联收藏引用数 |
