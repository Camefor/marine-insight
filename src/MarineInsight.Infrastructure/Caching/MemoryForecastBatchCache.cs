using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;
using Microsoft.Extensions.Caching.Memory;

namespace MarineInsight.Infrastructure.Caching;

public sealed class MemoryForecastBatchCache : IForecastBatchCache
{
    private readonly IMemoryCache _memoryCache;
    private readonly TimeProvider _timeProvider;

    public MemoryForecastBatchCache(IMemoryCache memoryCache, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _memoryCache = memoryCache;
        _timeProvider = timeProvider;
    }

    public Task<ForecastCacheEntry?> GetAsync(
        ForecastCacheKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_memoryCache.Get<ForecastCacheEntry>(key.Value));
    }

    public Task SetAsync(
        ForecastCacheKey key,
        ForecastBatch batch,
        ForecastCachePolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        var entry = new ForecastCacheEntry(batch, UtcNow, policy);
        _memoryCache.Set(
            key.Value,
            entry,
            new MemoryCacheEntryOptions
            {
                // Keep the entry through the stale-if-error window, then let the cache
                // remove it so expired data cannot be mistaken for an allowed fallback.
                AbsoluteExpiration = entry.StaleUntilUtc
            });

        return Task.CompletedTask;
    }

    private DateTimeOffset UtcNow => _timeProvider.GetUtcNow().ToUniversalTime();
}
