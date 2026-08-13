using System.Globalization;
using System.Net;
using System.Text.Json;
using MarineInsight.Application.Errors;
using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Providers.WorldTides;

public sealed class WorldTidesProvider(
    HttpClient httpClient,
    IOptions<WorldTidesOptions> options,
    IMemoryCache cache,
    TimeProvider timeProvider) : ITideProvider
{
    private const string Code = "worldtides";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderCode => Code;

    public bool IsEnabled => options.Value.Enabled;

    public async Task<ProviderTideResult> GetTidesAsync(GeoPoint location, ForecastRange range, CancellationToken cancellationToken)
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

        var response = await FetchAsync(location, range, settings, cancellationToken);
        var result = Normalize(response, location, range, timeProvider.GetUtcNow(), settings.CreditWarningThreshold);
        cache.Set(key, result, settings.CacheLifetime);
        return result;
    }

    private async Task<WorldTidesResponse> FetchAsync(GeoPoint location, ForecastRange range, WorldTidesOptions settings, CancellationToken cancellationToken)
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
            ["key"] = settings.ApiKey
        };
        var uri = QueryHelpers.AddQueryString(settings.BaseUrl, query);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.Timeout);
        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
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
            return await JsonSerializer.DeserializeAsync<WorldTidesResponse>(stream, JsonOptions, timeout.Token)
                ?? throw new ProviderContractException(Code, "WorldTides returned an empty response.");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderTimeoutException(Code, "WorldTides request timed out.", exception);
        }
        catch (JsonException exception)
        {
            throw new ProviderContractException(Code, "WorldTides response JSON is invalid.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderException(Code, ProviderFailureKind.Unavailable, "WorldTides could not be reached.", true, innerException: exception);
        }
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
            if (response.Error?.Contains("credit", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new ProviderException(Code, ProviderFailureKind.QuotaExceeded, "WorldTides credits are exhausted.", false);
            }

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
}
