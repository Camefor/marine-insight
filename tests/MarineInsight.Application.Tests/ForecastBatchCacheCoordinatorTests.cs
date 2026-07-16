using System.Collections.Concurrent;
using MarineInsight.Application.Errors;
using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Tests;

public sealed class ForecastBatchCacheCoordinatorTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);
    private static readonly GeoPoint Location = new(30.1234, 122.9876);
    private static readonly ProviderIdentity Provider = new("open-meteo", "best-match");
    private static readonly ForecastRange Range = new(StartUtc, 24);

    [Fact]
    public async Task FreshCacheHitDoesNotCallProvider()
    {
        var clock = new TestTimeProvider(StartUtc);
        var cache = new FakeForecastBatchCache(clock);
        var coordinator = new ForecastBatchCacheCoordinator(cache, clock);
        var key = CreateKey();
        var batch = CreateBatch();
        var policy = new ForecastCachePolicy(TimeSpan.FromMinutes(15), TimeSpan.FromHours(2));
        await cache.SetAsync(key, batch, policy);

        var providerCalls = 0;
        var result = await coordinator.GetOrCreateAsync(
            key,
            policy,
            _ =>
            {
                Interlocked.Increment(ref providerCalls);
                return Task.FromResult(batch);
            });

        Assert.Equal(ForecastCacheResultKind.FreshCache, result.Kind);
        Assert.Same(batch, result.Batch);
        Assert.Equal(0, providerCalls);
        Assert.Equal(batch.FetchedAtUtc, result.Batch.FetchedAtUtc);
    }

    [Fact]
    public async Task ProviderFailureUsesStaleBatchAndMarksEveryQualityLayer()
    {
        var clock = new TestTimeProvider(StartUtc);
        var cache = new FakeForecastBatchCache(clock);
        var coordinator = new ForecastBatchCacheCoordinator(cache, clock);
        var key = CreateKey();
        var batch = CreateBatch();
        var policy = new ForecastCachePolicy(TimeSpan.FromMinutes(15), TimeSpan.FromHours(2));
        await cache.SetAsync(key, batch, policy);
        clock.Advance(TimeSpan.FromMinutes(30));

        var result = await coordinator.GetOrCreateAsync(
            key,
            policy,
            _ => Task.FromException<ForecastBatch>(
                new ProviderTimeoutException("open-meteo", "provider timed out")));

        Assert.Equal(ForecastCacheResultKind.StaleCache, result.Kind);
        Assert.True(result.IsStale);
        Assert.Equal(TimeSpan.FromMinutes(30), result.CacheAge);
        Assert.Equal(batch.BatchId, result.Batch.BatchId);
        Assert.Equal(batch.FetchedAtUtc, result.Batch.FetchedAtUtc);
        Assert.Equal(ForecastQualityStatus.Stale, result.Batch.Quality.Status);
        Assert.Equal(ForecastFreshness.Stale, result.Batch.Quality.Freshness);
        Assert.True(result.Batch.Quality.Flags.HasFlag(ForecastQualityMask.StaleData));
        Assert.Equal(ForecastQualityStatus.Stale, result.Batch.Points[0].Quality.Status);
        Assert.Equal(ForecastQualityStatus.Stale, result.Batch.Points[0].MetricSources[0].QualityStatus);
    }

    [Fact]
    public async Task ProviderFailureAfterStaleWindowIsPropagated()
    {
        var clock = new TestTimeProvider(StartUtc);
        var cache = new FakeForecastBatchCache(clock);
        var coordinator = new ForecastBatchCacheCoordinator(cache, clock);
        var key = CreateKey();
        var policy = new ForecastCachePolicy(TimeSpan.FromMinutes(15), TimeSpan.FromHours(2));
        await cache.SetAsync(key, CreateBatch(), policy);
        clock.Advance(TimeSpan.FromHours(3));

        await Assert.ThrowsAsync<ProviderTimeoutException>(() => coordinator.GetOrCreateAsync(
            key,
            policy,
            _ => Task.FromException<ForecastBatch>(
                new ProviderTimeoutException("open-meteo", "provider timed out"))));
    }

    [Fact]
    public async Task ConcurrentMissesShareOneProviderRefresh()
    {
        var clock = new TestTimeProvider(StartUtc);
        var cache = new FakeForecastBatchCache(clock);
        var coordinator = new ForecastBatchCacheCoordinator(cache, clock);
        var key = CreateKey();
        var policy = new ForecastCachePolicy(TimeSpan.FromMinutes(15), TimeSpan.FromHours(2));
        var batch = CreateBatch();
        var providerStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProvider = new TaskCompletionSource<ForecastBatch>(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerCalls = 0;

        Task<ForecastBatch> FetchAsync(CancellationToken _)
        {
            Interlocked.Increment(ref providerCalls);
            providerStarted.TrySetResult(null);
            return releaseProvider.Task;
        }

        var requests = Enumerable.Range(0, 20)
            .Select(_ => coordinator.GetOrCreateAsync(key, policy, FetchAsync))
            .ToArray();
        await providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        releaseProvider.SetResult(batch);
        var results = await Task.WhenAll(requests);

        Assert.Equal(1, providerCalls);
        Assert.All(results, result => Assert.Equal(batch.BatchId, result.Batch.BatchId));
    }

    [Fact]
    public async Task ProviderBatchIdentityMustMatchCacheKey()
    {
        var clock = new TestTimeProvider(StartUtc);
        var cache = new FakeForecastBatchCache(clock);
        var coordinator = new ForecastBatchCacheCoordinator(cache, clock);
        var key = CreateKey();
        var policy = new ForecastCachePolicy(TimeSpan.FromMinutes(15), TimeSpan.FromHours(2));
        var wrongProviderBatch = CreateBatch(new ProviderIdentity("other-provider", "other-model"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.GetOrCreateAsync(
            key,
            policy,
            _ => Task.FromResult(wrongProviderBatch)));
    }

    [Fact]
    public async Task CacheBackendFailureDoesNotBlockProviderResult()
    {
        var clock = new TestTimeProvider(StartUtc);
        var coordinator = new ForecastBatchCacheCoordinator(new UnavailableCache(), clock);
        var key = CreateKey();
        var policy = new ForecastCachePolicy(TimeSpan.FromMinutes(15), TimeSpan.FromHours(2));
        var batch = CreateBatch();

        var result = await coordinator.GetOrCreateAsync(
            key,
            policy,
            _ => Task.FromResult(batch));

        Assert.Equal(ForecastCacheResultKind.Provider, result.Kind);
        Assert.Equal(batch.BatchId, result.Batch.BatchId);
    }

    private static ForecastCacheKey CreateKey(
        ProviderIdentity? provider = null,
        ForecastRange? range = null) =>
        ForecastCacheKey.Create(
            "test",
            ForecastDataDomain.Weather,
            provider ?? Provider,
            Location,
            range ?? Range,
            "v1");

    private static ForecastBatch CreateBatch(ProviderIdentity? provider = null)
    {
        var batchProvider = provider ?? Provider;
        var batchId = Guid.NewGuid();
        var quality = DataQuality.Valid();
        var time = Range.StartUtc;
        var source = new MetricSource(
            ForecastMetricName.WindSpeedMs,
            batchProvider,
            batchId,
            time,
            quality.Status,
            quality.Freshness);
        var point = new ForecastPoint(
            time,
            ForecastMetricSet.Create(windSpeedMs: 4),
            quality,
            [source]);

        return new ForecastBatch(
            batchId,
            ForecastDataDomain.Weather,
            batchProvider,
            Location,
            null,
            StartUtc.AddHours(-1),
            StartUtc,
            Range,
            [point],
            quality);
    }

    private sealed class FakeForecastBatchCache(TestTimeProvider clock) : IForecastBatchCache
    {
        private readonly ConcurrentDictionary<ForecastCacheKey, ForecastCacheEntry> _entries = [];

        public Task<ForecastCacheEntry?> GetAsync(
            ForecastCacheKey key,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _entries.TryGetValue(key, out var entry);
            return Task.FromResult(entry);
        }

        public Task SetAsync(
            ForecastCacheKey key,
            ForecastBatch batch,
            ForecastCachePolicy policy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _entries[key] = new ForecastCacheEntry(batch, clock.GetUtcNow(), policy);
            return Task.CompletedTask;
        }
    }

    private sealed class UnavailableCache : IForecastBatchCache
    {
        public Task<ForecastCacheEntry?> GetAsync(
            ForecastCacheKey key,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ForecastCacheEntry?>(
                new CacheUnavailableException("Cache backend is unavailable."));

        public Task SetAsync(
            ForecastCacheKey key,
            ForecastBatch batch,
            ForecastCachePolicy policy,
            CancellationToken cancellationToken = default) =>
            Task.FromException(
                new CacheUnavailableException("Cache backend is unavailable."));
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow.ToUniversalTime();

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
