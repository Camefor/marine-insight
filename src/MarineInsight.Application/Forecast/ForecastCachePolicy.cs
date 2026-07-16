namespace MarineInsight.Application.Forecast;

/// <summary>
/// Controls the fresh period and the bounded stale-if-error period for one cache entry.
/// </summary>
public readonly record struct ForecastCachePolicy
{
    public ForecastCachePolicy(TimeSpan freshLifetime, TimeSpan staleIfErrorLifetime)
    {
        if (freshLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(freshLifetime),
                freshLifetime,
                "Fresh cache lifetime must be positive.");
        }

        if (staleIfErrorLifetime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(staleIfErrorLifetime),
                staleIfErrorLifetime,
                "Stale-if-error lifetime cannot be negative.");
        }

        FreshLifetime = freshLifetime;
        StaleIfErrorLifetime = staleIfErrorLifetime;
    }

    public TimeSpan FreshLifetime { get; }

    public TimeSpan StaleIfErrorLifetime { get; }

    public TimeSpan EntryLifetime => FreshLifetime + StaleIfErrorLifetime;
}
