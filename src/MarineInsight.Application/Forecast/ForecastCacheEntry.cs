using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast;

/// <summary>
/// Stores a batch together with cache-clock boundaries. The batch's provider fetched time
/// remains the source timestamp and is deliberately not replaced by the cache timestamp.
/// </summary>
public sealed record ForecastCacheEntry
{
    public ForecastCacheEntry(
        ForecastBatch batch,
        DateTimeOffset cachedAtUtc,
        ForecastCachePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(batch);

        CachedAtUtc = cachedAtUtc.ToUniversalTime();
        FreshUntilUtc = CachedAtUtc.Add(policy.FreshLifetime);
        StaleUntilUtc = FreshUntilUtc.Add(policy.StaleIfErrorLifetime);
        Batch = batch;
    }

    public ForecastBatch Batch { get; }

    public DateTimeOffset CachedAtUtc { get; }

    public DateTimeOffset FreshUntilUtc { get; }

    public DateTimeOffset StaleUntilUtc { get; }

    public bool IsFresh(DateTimeOffset nowUtc) =>
        nowUtc.ToUniversalTime() < FreshUntilUtc;

    public bool IsWithinStaleIfError(DateTimeOffset nowUtc)
    {
        var now = nowUtc.ToUniversalTime();
        return now >= FreshUntilUtc && now < StaleUntilUtc;
    }

    public TimeSpan GetAge(DateTimeOffset nowUtc)
    {
        var age = nowUtc.ToUniversalTime() - CachedAtUtc;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }
}
