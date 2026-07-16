using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast.Ports;

/// <summary>
/// Cache port for normalized forecast batches. Implementations may be L1 or L2 stores;
/// cache failures must remain non-authoritative for the business result.
/// </summary>
public interface IForecastBatchCache
{
    Task<ForecastCacheEntry?> GetAsync(
        ForecastCacheKey key,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        ForecastCacheKey key,
        ForecastBatch batch,
        ForecastCachePolicy policy,
        CancellationToken cancellationToken = default);
}
