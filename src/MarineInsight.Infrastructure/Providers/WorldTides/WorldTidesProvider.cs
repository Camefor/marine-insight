using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using MarineInsight.Application.Credentials.Ports;
using MarineInsight.Application.Errors;
using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Application.ProviderCalls;
using MarineInsight.Application.ProviderCalls.Ports;
using MarineInsight.Domain.Forecast;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Providers.WorldTides;

/// <summary>后台「测试连接」结果，直接用于管理界面展示。</summary>
public sealed record WorldTidesKeyTestResult(
    bool Success,
    string Message,
    int? RemainingCredits);

public sealed class WorldTidesProvider(
    HttpClient httpClient,
    IOptions<WorldTidesOptions> options,
    IMemoryCache cache,
    IProviderCredentialStore credentialStore,
    IProviderCallLogStore callLogStore,
    TimeProvider timeProvider,
    ILogger<WorldTidesProvider> logger) : ITideProvider
{
    private const string Code = "worldtides";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Action<ILogger, Guid, string, Exception?> LogCallCompletionFailure =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            new EventId(6101, "ProviderCallLogCompletionFailed"),
            "Failed to complete paid provider call log {ProviderCallId} for {ProviderCode}.");
    private static readonly Action<ILogger, string, Exception?> LogCredentialUnavailable =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2101, "ProviderCredentialUnavailable"),
            "No usable credential is configured for provider {ProviderCode}; tide data is unavailable.");
    private static readonly Action<ILogger, string, string, Exception?> LogCredentialRejected =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2102, "ProviderCredentialRejected"),
            "All configured credentials were rejected by provider {ProviderCode}; failure code {ErrorCode}.");

    public string ProviderCode => Code;

    public bool IsEnabled => options.Value.Enabled;

    public async Task<ProviderTideResult> GetTidesAsync(
        GeoPoint location,
        ForecastRange range,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        settings.Validate();
        if (!settings.Enabled)
        {
            throw new ProviderException(Code, ProviderFailureKind.Unavailable, "WorldTides is disabled.", false);
        }

        // The normalized batch is clipped to the exact requested range, so the cache identity
        // must include UTC hours rather than only calendar dates.
        var key = FormattableString.Invariant($"mi:tide:v1:{location.Latitude:F4}:{location.Longitude:F4}:{range.StartUtc:yyyyMMddHH}:{range.EndUtc:yyyyMMddHH}");
        if (cache.TryGetValue<ProviderTideResult>(key, out var cached) && cached is not null)
        {
            return new ProviderTideResult(cached.Batch, true, cached.RemainingCredits, cached.CreditWarning);
        }

        var response = await FetchWithFailoverAsync(location, range, actorUserId, settings, cancellationToken);
        var result = Normalize(response, location, range, timeProvider.GetUtcNow(), settings.CreditWarningThreshold);
        cache.Set(key, result, settings.CacheLifetime);
        return result;
    }

    /// <summary>用候选密钥真实调用一次 WorldTides 验证连通性，供后台添加密钥前测试。</summary>
    public async Task<WorldTidesKeyTestResult> ValidateKeyAsync(
        Guid actorUserId,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new WorldTidesKeyTestResult(false, "API key 不能为空。", null);
        }

        var normalizedApiKey = apiKey.Trim();
        var requestedAt = timeProvider.GetUtcNow();
        var query = new Dictionary<string, string?>
        {
            ["heights"] = string.Empty,
            ["lat"] = "30.194000",
            ["lon"] = "122.687000",
            ["date"] = requestedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["days"] = "1",
            ["key"] = normalizedApiKey
        };
        var uri = QueryHelpers.AddQueryString(settings.BaseUrl, query);
        var callId = await callLogStore.BeginAsync(new StartProviderCallLog(
            actorUserId,
            Code,
            ProviderCallOperations.CredentialValidation,
            null,
            CredentialHint(normalizedApiKey),
            30.19,
            122.69,
            new DateTimeOffset(requestedAt.UtcDateTime.Date, TimeSpan.Zero),
            new DateTimeOffset(requestedAt.UtcDateTime.Date.AddDays(1), TimeSpan.Zero),
            1,
            Activity.Current?.TraceId.ToString()), cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        int? httpStatusCode = null;
        WorldTidesResponse? payload = null;
        var succeeded = false;
        string? errorCode = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.Timeout);
        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            httpStatusCode = (int)response.StatusCode;
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                errorCode = MarineInsightErrorCodes.ProviderAuthenticationFailed;
                return new WorldTidesKeyTestResult(false, "WorldTides 拒绝了该 Key（401/403）。", null);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                errorCode = MarineInsightErrorCodes.RateLimited;
                return new WorldTidesKeyTestResult(false, "WorldTides 请求被限流（429）。", null);
            }

            if (!response.IsSuccessStatusCode)
            {
                errorCode = MarineInsightErrorCodes.ProviderUnavailable;
                return new WorldTidesKeyTestResult(false, $"WorldTides 返回 HTTP {(int)response.StatusCode}。", null);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            payload = await JsonSerializer.DeserializeAsync<WorldTidesResponse>(stream, JsonOptions, timeout.Token);
            if (payload is null)
            {
                errorCode = MarineInsightErrorCodes.ProviderContractInvalid;
                return new WorldTidesKeyTestResult(false, "WorldTides 返回了空响应。", null);
            }

            if (payload.Status is >= 400 || !string.IsNullOrWhiteSpace(payload.Error))
            {
                if (payload.Error?.Contains("credit", StringComparison.OrdinalIgnoreCase) == true)
                {
                    errorCode = MarineInsightErrorCodes.ProviderQuotaExceeded;
                    return new WorldTidesKeyTestResult(false, "Key 有效但 WorldTides 额度已耗尽。", payload.RemainingCredits);
                }

                errorCode = MarineInsightErrorCodes.ProviderContractInvalid;
                return new WorldTidesKeyTestResult(false, $"WorldTides 返回错误：{payload.Error ?? $"status {payload.Status}"}", payload.RemainingCredits);
            }

            succeeded = true;
            return new WorldTidesKeyTestResult(true, "连接成功，Key 有效。", payload.RemainingCredits);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            errorCode = MarineInsightErrorCodes.ProviderUnavailable;
            return new WorldTidesKeyTestResult(false, "WorldTides 请求超时。", null);
        }
        catch (HttpRequestException)
        {
            errorCode = MarineInsightErrorCodes.ProviderUnavailable;
            return new WorldTidesKeyTestResult(false, "无法连接 WorldTides。", null);
        }
        catch (JsonException)
        {
            errorCode = MarineInsightErrorCodes.ProviderContractInvalid;
            return new WorldTidesKeyTestResult(false, "WorldTides 响应不是有效 JSON。", null);
        }
        finally
        {
            stopwatch.Stop();
            await CompleteCallSafelyAsync(
                callId,
                succeeded,
                httpStatusCode,
                payload,
                stopwatch.ElapsedMilliseconds,
                errorCode);
        }
    }

    private async Task<WorldTidesResponse> FetchWithFailoverAsync(
        GeoPoint location,
        ForecastRange range,
        Guid actorUserId,
        WorldTidesOptions settings,
        CancellationToken cancellationToken)
    {
        var candidates = await ResolveCandidatesAsync(settings, cancellationToken);
        ProviderException? lastKeyFailure = null;
        foreach (var candidate in candidates)
        {
            try
            {
                var response = await FetchAsync(location, range, actorUserId, settings, candidate, cancellationToken);
                if (IsQuotaExhausted(response, out var quotaReason))
                {
                    await ReportHealthAsync(
                        candidate,
                        success: false,
                        response.RemainingCredits,
                        settings.CreditWarningThreshold,
                        quotaReason,
                        cancellationToken);
                    lastKeyFailure = new ProviderException(Code, ProviderFailureKind.QuotaExceeded, quotaReason, false);
                    continue;
                }

                await ReportHealthAsync(
                    candidate,
                    success: true,
                    response.RemainingCredits,
                    settings.CreditWarningThreshold,
                    null,
                    cancellationToken);
                return response;
            }
            catch (ProviderAuthenticationException exception)
            {
                lastKeyFailure = exception;
                await ReportHealthAsync(candidate, false, null, settings.CreditWarningThreshold, exception.Message, cancellationToken);
            }
            catch (ProviderRateLimitedException exception)
            {
                lastKeyFailure = exception;
                await ReportHealthAsync(candidate, false, null, settings.CreditWarningThreshold, exception.Message, cancellationToken);
            }
        }

        if (lastKeyFailure is not null)
        {
            LogCredentialRejected(logger, Code, lastKeyFailure.ErrorCode, lastKeyFailure);
            throw lastKeyFailure;
        }

        var noCredentialException = new ProviderAuthenticationException(Code, "WorldTides API key is not configured.");
        LogCredentialUnavailable(logger, Code, noCredentialException);
        throw noCredentialException;
    }

    private async Task<IReadOnlyList<CredentialCandidate>> ResolveCandidatesAsync(
        WorldTidesOptions settings,
        CancellationToken cancellationToken)
    {
        var secrets = await credentialStore.ListSecretsAsync(Code, cancellationToken);
        var candidates = new List<CredentialCandidate>(secrets.Count + 1);
        foreach (var secret in secrets)
        {
            candidates.Add(new CredentialCandidate(secret.Id, CredentialHint(secret.ApiKey), secret.ApiKey));
        }

        // 配置兜底：无 DB 密钥时使用注入的 ApiKey（User Secrets / key-per-file），不记健康。
        if (secrets.Count == 0 && !string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            candidates.Add(new CredentialCandidate(null, CredentialHint(settings.ApiKey), settings.ApiKey));
        }

        return candidates;
    }

    private async Task ReportHealthAsync(
        CredentialCandidate candidate,
        bool success,
        int? remainingCredits,
        int creditWarningThreshold,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        var creditWarning = remainingCredits.HasValue && remainingCredits.Value <= creditWarningThreshold;
        await credentialStore.ReportHealthAsync(candidate.KeyId, success, remainingCredits, creditWarning, failureReason, cancellationToken);
    }

    private async Task<WorldTidesResponse> FetchAsync(
        GeoPoint location,
        ForecastRange range,
        Guid actorUserId,
        WorldTidesOptions settings,
        CredentialCandidate candidate,
        CancellationToken cancellationToken)
    {
        var days = Math.Max(1, (int)Math.Ceiling((range.EndUtc.Date - range.StartUtc.Date).TotalDays) + 1);
        var query = new Dictionary<string, string?>
        {
            ["heights"] = string.Empty,
            ["extremes"] = string.Empty,
            ["lat"] = location.Latitude.ToString("F6", CultureInfo.InvariantCulture),
            ["lon"] = location.Longitude.ToString("F6", CultureInfo.InvariantCulture),
            ["date"] = range.StartUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["days"] = days.ToString(CultureInfo.InvariantCulture),
            ["key"] = candidate.ApiKey
        };
        var uri = QueryHelpers.AddQueryString(settings.BaseUrl, query);
        // 先落 started 记录再出网：若费用日志存储不可用，则不发起可能消耗 Credits 的请求。
        // 坐标仅保留两位小数桶，既能定位费用来源区域，又避免在运维日志保存精确位置。
        var callId = await callLogStore.BeginAsync(new StartProviderCallLog(
            actorUserId,
            Code,
            ProviderCallOperations.TideForecast,
            candidate.KeyId,
            candidate.KeyHint,
            Math.Round(location.Latitude, 2),
            Math.Round(location.Longitude, 2),
            range.StartUtc,
            range.EndUtc,
            days,
            Activity.Current?.TraceId.ToString()), cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        int? httpStatusCode = null;
        WorldTidesResponse? payload = null;
        var succeeded = false;
        string? errorCode = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.Timeout);
        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            httpStatusCode = (int)response.StatusCode;
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new ProviderAuthenticationException(Code, "WorldTides rejected the configured credential.");
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new ProviderRateLimitedException(Code, "WorldTides rate-limited the request.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new ProviderException(Code, ProviderFailureKind.Unavailable, $"WorldTides returned HTTP {(int)response.StatusCode}.", (int)response.StatusCode >= 500);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            payload = await JsonSerializer.DeserializeAsync<WorldTidesResponse>(stream, JsonOptions, timeout.Token)
                ?? throw new ProviderContractException(Code, "WorldTides returned an empty response.");
            succeeded = payload.Status is < 400 && string.IsNullOrWhiteSpace(payload.Error);
            errorCode = succeeded
                ? null
                : payload.Error?.Contains("credit", StringComparison.OrdinalIgnoreCase) == true
                    ? MarineInsightErrorCodes.ProviderQuotaExceeded
                    : MarineInsightErrorCodes.ProviderContractInvalid;
            return payload;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            errorCode = MarineInsightErrorCodes.ProviderUnavailable;
            throw new ProviderTimeoutException(Code, "WorldTides request timed out.", exception);
        }
        catch (JsonException exception)
        {
            errorCode = MarineInsightErrorCodes.ProviderContractInvalid;
            throw new ProviderContractException(Code, "WorldTides response JSON is invalid.", exception);
        }
        catch (HttpRequestException exception)
        {
            errorCode = MarineInsightErrorCodes.ProviderUnavailable;
            throw new ProviderException(Code, ProviderFailureKind.Unavailable, "WorldTides could not be reached.", true, innerException: exception);
        }
        catch (ProviderException exception)
        {
            errorCode = exception.ErrorCode;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            await CompleteCallSafelyAsync(
                callId,
                succeeded,
                httpStatusCode,
                payload,
                stopwatch.ElapsedMilliseconds,
                errorCode);
        }
    }

    private static bool IsQuotaExhausted(WorldTidesResponse response, out string reason)
    {
        reason = string.Empty;
        if (response.Status is < 400 && string.IsNullOrWhiteSpace(response.Error))
        {
            return false;
        }

        if (response.Error?.Contains("credit", StringComparison.OrdinalIgnoreCase) == true)
        {
            reason = "WorldTides credits are exhausted.";
            return true;
        }

        return false;
    }

    private static ProviderTideResult Normalize(
        WorldTidesResponse response,
        GeoPoint requested,
        ForecastRange range,
        DateTimeOffset fetchedAt,
        int creditWarningThreshold)
    {
        if (response.Status is >= 400 || !string.IsNullOrWhiteSpace(response.Error))
        {
            throw new ProviderContractException(Code, "WorldTides reported an application error.");
        }
        var heights = response.Heights?.Where(item => double.IsFinite(item.HeightM))
            .Select(item => (Time: DateTimeOffset.FromUnixTimeSeconds(item.Timestamp), item.HeightM))
            .Where(item => range.Contains(item.Time))
            .OrderBy(item => item.Time)
            .ToArray() ?? [];
        if (heights.Length == 0)
        {
            throw new ProviderContractException(Code, "WorldTides returned no heights in the requested range.");
        }

        var batchId = Guid.NewGuid();
        var provider = new ProviderIdentity(Code, "worldtides-v3");
        var extremes = response.Extremes ?? [];
        var points = heights.Select(item =>
        {
            var tideType = FindType(item.Time, extremes);
            var metrics = ForecastMetricSet.Create(tideHeightM: item.HeightM, tideType: tideType);
            var sources = metrics.GetPresentMetrics().Select(metric => new MetricSource(
                metric, provider, batchId, item.Time, ForecastQualityStatus.Valid, ForecastFreshness.Fresh));
            return new ForecastPoint(item.Time, metrics, DataQuality.Valid(), sources);
        }).ToArray();
        GeoPoint? grid = response.ResponseLatitude.HasValue && response.ResponseLongitude.HasValue
            ? new GeoPoint(response.ResponseLatitude.Value, response.ResponseLongitude.Value)
            : null;
        var batch = new ForecastBatch(batchId, ForecastDataDomain.Tide, provider, requested, grid,
            fetchedAt, fetchedAt, range, points, DataQuality.Valid());
        var creditWarning = response.RemainingCredits.HasValue && response.RemainingCredits.Value <= creditWarningThreshold;
        return new ProviderTideResult(batch, false, response.RemainingCredits, creditWarning);
    }

    private static TideType? FindType(DateTimeOffset time, IEnumerable<WorldTidesExtreme> extremes)
    {
        var nearest = extremes.Select(item => new { Item = item, Distance = Math.Abs((DateTimeOffset.FromUnixTimeSeconds(item.Timestamp) - time).TotalMinutes) })
            .Where(item => item.Distance <= 30)
            .OrderBy(item => item.Distance)
            .FirstOrDefault()?.Item;
        return nearest?.Type?.ToLowerInvariant() switch { "high" => TideType.High, "low" => TideType.Low, _ => null };
    }

    private async Task CompleteCallSafelyAsync(
        Guid callId,
        bool succeeded,
        int? httpStatusCode,
        WorldTidesResponse? payload,
        long durationMs,
        string? errorCode)
    {
        try
        {
            await callLogStore.CompleteAsync(callId, new CompleteProviderCallLog(
                succeeded,
                httpStatusCode,
                payload?.CallCount,
                payload?.RemainingCredits,
                durationMs,
                errorCode), CancellationToken.None);
        }
        catch (Exception exception)
        {
            // started 行已经持久化；完成更新失败时保留为 started，供后台识别并排查，且不得以日志异常覆盖 Provider 原始结果。
            LogCallCompletionFailure(logger, callId, Code, exception);
        }
    }

    private static string CredentialHint(string apiKey)
    {
        var normalized = apiKey.Trim();
        var suffix = normalized.Length <= 4 ? normalized : normalized[^4..];
        return $"••••{suffix}";
    }

    private sealed record CredentialCandidate(Guid? KeyId, string KeyHint, string ApiKey);
}
