using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast;

public enum ForecastCacheResultKind
{
    Provider,
    FreshCache,
    StaleCache
}

/// <summary>
/// Describes how a normalized batch was obtained so API/UI layers can expose degradation explicitly.
/// </summary>
public sealed record ForecastCacheResult
{
    public ForecastCacheResult(
        ForecastBatch batch,
        ForecastCacheResultKind kind,
        TimeSpan? cacheAge)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown forecast cache result kind.");
        }

        if (cacheAge is { } age && age < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cacheAge), cacheAge, "Cache age cannot be negative.");
        }

        Batch = batch;
        Kind = kind;
        CacheAge = cacheAge;
    }

    public ForecastBatch Batch { get; }

    public ForecastCacheResultKind Kind { get; }

    public TimeSpan? CacheAge { get; }

    public bool IsFromCache => Kind is ForecastCacheResultKind.FreshCache or ForecastCacheResultKind.StaleCache;

    public bool IsStale => Kind == ForecastCacheResultKind.StaleCache;
}
