using System.Collections.Concurrent;
using System.Threading;
using MarineInsight.Application.Errors;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast;

/// <summary>
/// Coordinates cache-aside reads and single-flight provider refreshes for one cache key.
/// </summary>
public sealed class ForecastBatchCacheCoordinator
{
    private readonly IForecastBatchCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, Lazy<Task<ForecastCacheResult>>> _inFlight = [];

    public ForecastBatchCacheCoordinator(IForecastBatchCache cache, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _cache = cache;
        _timeProvider = timeProvider;
    }

    public async Task<ForecastCacheResult> GetOrCreateAsync(
        ForecastCacheKey key,
        ForecastCachePolicy policy,
        Func<CancellationToken, Task<ForecastBatch>> fetchAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fetchAsync);

        ForecastCacheEntry? cached;
        try
        {
            cached = await _cache.GetAsync(key, cancellationToken);
        }
        catch (CacheUnavailableException)
        {
            // A cache outage is a miss; the provider remains the authoritative source.
            cached = null;
        }
        var now = UtcNow;
        if (cached is not null && cached.IsFresh(now))
        {
            return new ForecastCacheResult(
                cached.Batch,
                ForecastCacheResultKind.FreshCache,
                cached.GetAge(now));
        }

        var candidate = new Lazy<Task<ForecastCacheResult>>(
            () => RefreshAsync(key, policy, cached, fetchAsync),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var operation = _inFlight.GetOrAdd(key.Value, candidate);
        var operationTask = operation.Value;

        try
        {
            // A cancelled waiter stops waiting, but does not cancel the shared refresh.
            // This allows another request to benefit from a provider call already in flight.
            return await operationTask.WaitAsync(cancellationToken);
        }
        finally
        {
            if (operationTask.IsCompleted &&
                _inFlight.TryGetValue(key.Value, out var current) &&
                ReferenceEquals(current, operation))
            {
                _inFlight.TryRemove(key.Value, out _);
            }
        }
    }

    private async Task<ForecastCacheResult> RefreshAsync(
        ForecastCacheKey key,
        ForecastCachePolicy policy,
        ForecastCacheEntry? initialCached,
        Func<CancellationToken, Task<ForecastBatch>> fetchAsync)
    {
        try
        {
            // The provider contract already owns its bounded timeout. The shared refresh
            // intentionally outlives an individual HTTP waiter and populates L1 for peers.
            var batch = await fetchAsync(CancellationToken.None);
            ArgumentNullException.ThrowIfNull(batch);

            if (!key.Matches(batch))
            {
                throw new InvalidOperationException(
                    "The provider batch does not match the forecast cache key identity.");
            }

            try
            {
                await _cache.SetAsync(key, batch, policy, CancellationToken.None);
            }
            catch (CacheUnavailableException)
            {
                // Do not turn a successful provider result into a failed business query.
            }

            return new ForecastCacheResult(batch, ForecastCacheResultKind.Provider, null);
        }
        catch (ProviderException)
        {
            var stale = await GetStaleEntryAsync(key, initialCached);
            if (stale is null)
            {
                throw;
            }

            var now = UtcNow;
            return new ForecastCacheResult(
                stale.Batch.AsStale(),
                ForecastCacheResultKind.StaleCache,
                stale.GetAge(now));
        }
    }

    private async Task<ForecastCacheEntry?> GetStaleEntryAsync(
        ForecastCacheKey key,
        ForecastCacheEntry? initialCached)
    {
        ForecastCacheEntry? latest;
        try
        {
            latest = await _cache.GetAsync(key, CancellationToken.None);
        }
        catch (CacheUnavailableException)
        {
            latest = null;
        }

        latest ??= initialCached;
        return latest is not null && latest.IsWithinStaleIfError(UtcNow)
            ? latest
            : null;
    }

    private DateTimeOffset UtcNow => _timeProvider.GetUtcNow().ToUniversalTime();
}
