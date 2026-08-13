using MarineInsight.Application.Analysis;
using MarineInsight.Application.Analysis.Ports;
using Microsoft.Extensions.Caching.Memory;

namespace MarineInsight.Infrastructure.Providers.Explanation;

public sealed class MemoryExplanationCache(IMemoryCache memoryCache) : IExplanationCache
{
    public Task<AnalysisExplanation?> GetAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(memoryCache.Get<AnalysisExplanation>(key));
    }

    public Task SetAsync(
        string key,
        AnalysisExplanation explanation,
        ExplanationCachePolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(explanation);
        cancellationToken.ThrowIfCancellationRequested();

        memoryCache.Set(
            key,
            explanation,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = policy.CacheLifetime
            });

        return Task.CompletedTask;
    }
}
