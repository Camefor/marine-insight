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
| 分析 | GET | `/marine-analyses/{id}` | 获取已生成结果 | 匿名/所有者策略 |
| 预报 | GET | `/forecast-batches/{id}/points` | 获取逐小时标准预报 | 匿名 |
| 收藏 | GET | `/favorites` | 收藏列表 | 登录 |
| 收藏 | POST | `/favorites` | 新增收藏 | 登录 |
| 收藏 | PUT | `/favorites/{id}` | 修改默认活动/备注/排序 | 登录 |
| 收藏 | DELETE | `/favorites/{id}` | 删除收藏 | 登录 |
| 历史 | GET | `/query-history` | 查询历史 | 登录 |
| 设置 | GET/PUT | `/user-settings` | 用户单位和偏好 | 登录 |
| 管理 | GET | `/admin/providers` | Provider 状态与配额摘要 | 管理员 |
| 管理 | POST | `/admin/algorithms/{id}/publish` | 发布算法版本 | 管理员 |
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

### 5.2 响应

```json
{
  "analysisId": "82f9ff76-bb11-4456-93e5-78d669e609ea",
  "sourceBatchIds": [
    "35d4af68-6297-46d2-bc7d-fc0d62bb79bd",
    "bea327c7-6039-43b9-81f1-c1559a0c4987"
  ],
  "location": {
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

## 6. 地点搜索

`GET /api/v1/locations/search?q=东极岛&limit=10`

响应字段包括 `id`、`displayName`、`locationType`、`latitude`、`longitude`、`timeZone` 和 `source`。模糊候选不得自动替用户选择，重名地点必须显示行政区域或坐标。

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

## 8. 错误响应

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
| `RATE_LIMITED` | 429 | 请求超过配额 |
| `PROVIDER_UNAVAILABLE` | 503 | 外部数据源且缓存均不可用 |
| `AI_EXPLANATION_UNAVAILABLE` | 200/降级标记 | AI 失败但规则结果仍可返回 |

## 9. 缓存与条件请求

- 地点搜索可缓存 24 小时；预报按 Provider 更新节奏缓存 10-30 分钟。
- 分析响应的 ETag 由来源批次集合哈希、来源选择策略、算法版本、活动和单位偏好组成。
- 客户端可发送 `If-None-Match`；服务端返回 `304` 时不重复传输结果。
- 任何缓存响应都必须保留原数据 `fetchedAt`，不能将命中时间伪装为数据时间。

## 10. 限流

| 场景 | 初始策略 |
| --- | --- |
| 匿名分析 | 每 IP 每分钟 10 次、每日软配额 |
| 登录分析 | 每用户每分钟 30 次 |
| 地点搜索 | 每 IP 每分钟 60 次 |
| 管理发布 | 每用户每分钟 5 次并审计 |

服务端根据 Provider 配额动态收紧回源，不影响缓存命中读取。

## 11. 版本与废弃

- URL 主版本用于破坏性协议变化，例如 `/api/v2`。
- 新增可选字段不升级主版本；删除或改变语义需要新版本。
- 废弃接口至少保留一个发布周期，并返回 `Deprecation` 与 `Sunset` 响应头。
- 算法版本通过响应字段独立管理，不等同于 API 版本。

## 12. 变更记录

| 版本 | 日期 | 变更说明 |
| --- | --- | --- |
| 1.0 | 2026-07-13 | 定义版本化业务 API、ProblemDetails 和分析响应 |
| 1.1 | 2026-07-13 | 分析响应改为返回 Open-Meteo 等多个来源批次及数据域 |
