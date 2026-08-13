namespace MarineInsight.Application.Analysis;

public sealed record ExplanationCachePolicy
{
    public ExplanationCachePolicy(TimeSpan cacheLifetime)
    {
        if (cacheLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cacheLifetime),
                cacheLifetime,
                "Explanation cache lifetime must be positive.");
        }

        CacheLifetime = cacheLifetime;
    }

    public TimeSpan CacheLifetime { get; }

    public static ExplanationCachePolicy Default { get; } = new(TimeSpan.FromHours(24));
}
