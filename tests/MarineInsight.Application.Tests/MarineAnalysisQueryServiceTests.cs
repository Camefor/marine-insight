using System.Collections.Concurrent;
using MarineInsight.Application.Analysis;
using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Tests;

public sealed class MarineAnalysisQueryServiceTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);
    private static readonly GeoPoint Location = new(30.194, 122.687);
    private static readonly ForecastRange Range = new(StartUtc, 24);

    [Fact]
    public async Task ExecuteFetchesWeatherAndMarineThenUsesBothL1Entries()
    {
        var clock = new TestTimeProvider(StartUtc);
        var cache = new FakeForecastBatchCache(clock);
        var weather = new FakeWeatherProvider();
        var marine = new FakeMarineProvider();
        var service = new MarineAnalysisQueryService(
            weather,
            marine,
            new FakeCacheKeyFactory(),
            new ForecastBatchCacheCoordinator(cache, clock),
            new ForecastSnapshotAssembler());
        var query = new MarineAnalysisQuery(Location, Range);

        var first = await service.ExecuteAsync(query);
        var second = await service.ExecuteAsync(query);

        Assert.Equal(1, weather.CallCount);
        Assert.Equal(1, marine.CallCount);
        Assert.Equal(ForecastCacheResultKind.Provider, first.Weather.Kind);
        Assert.Equal(ForecastCacheResultKind.Provider, first.Marine.Kind);
        Assert.Equal(ForecastCacheResultKind.FreshCache, second.Weather.Kind);
        Assert.Equal(ForecastCacheResultKind.FreshCache, second.Marine.Kind);
        Assert.Equal(2, first.Snapshot.SourceBatches.Count);
        Assert.Equal(25, first.Snapshot.Points.Count);
        Assert.Equal(4, first.Snapshot.Points[0].Metrics.WindSpeedMs);
        Assert.Equal(0.8, first.Snapshot.Points[0].Metrics.WaveHeightM);
        Assert.Equal(ForecastQualityStatus.Valid, first.Snapshot.Quality.Status);
        Assert.Equal(25, first.HourlyAssessments.Count);
        Assert.Equal(5, first.HourlyAssessments[0].ActivityAssessments.Count);
        Assert.Equal("marine-score-1.0.0", first.HourlyAssessments[0].AlgorithmVersion);
    }

    private sealed class FakeCacheKeyFactory : IForecastCacheKeyFactory
    {
        public ForecastCachePolicy Policy { get; } = new(TimeSpan.FromMinutes(15), TimeSpan.FromHours(2));

        public ForecastCacheKey Create(
            ForecastDataDomain dataDomain,
            ProviderIdentity provider,
            GeoPoint location,
            ForecastRange range) =>
            ForecastCacheKey.Create("test", dataDomain, provider, location, range, "v1");
    }

    private sealed class FakeForecastBatchCache(TestTimeProvider clock) : IForecastBatchCache
    {
        private readonly ConcurrentDictionary<string, ForecastCacheEntry> _entries = [];

        public Task<ForecastCacheEntry?> GetAsync(
            ForecastCacheKey key,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _entries.TryGetValue(key.Value, out var entry);
            return Task.FromResult(entry);
        }

        public Task SetAsync(
            ForecastCacheKey key,
            ForecastBatch batch,
            ForecastCachePolicy policy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _entries[key.Value] = new ForecastCacheEntry(batch, clock.GetUtcNow(), policy);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWeatherProvider : IWeatherForecastProvider
    {
        public string ProviderCode => "fake-weather";

        public ProviderIdentity Identity { get; } = new("fake-weather", "configured-weather");

        public int CallCount { get; private set; }

        public Task<ProviderForecastResult> GetWeatherAsync(
            GeoPoint location,
            ForecastRange range,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new ProviderForecastResult(CreateBatch(
                ForecastDataDomain.Weather,
                Identity,
                location,
                range,
                ForecastMetricSet.Create(windSpeedMs: 4))));
        }
    }

    private sealed class FakeMarineProvider : IMarineForecastProvider
    {
        public string ProviderCode => "fake-marine";

        public ProviderIdentity Identity { get; } = new("fake-marine", "configured-marine");

        public int CallCount { get; private set; }

        public Task<ProviderForecastResult> GetMarineAsync(
            GeoPoint location,
            ForecastRange range,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new ProviderForecastResult(CreateBatch(
                ForecastDataDomain.Marine,
                Identity,
                location,
                range,
                ForecastMetricSet.Create(waveHeightM: 0.8))));
        }
    }

    private static ForecastBatch CreateBatch(
        ForecastDataDomain dataDomain,
        ProviderIdentity provider,
        GeoPoint location,
        ForecastRange range,
        ForecastMetricSet metrics)
    {
        var batchId = Guid.NewGuid();
        var points = Enumerable.Range(0, range.Hours + 1)
            .Select(index =>
            {
                var time = range.StartUtc.AddHours(index);
                var quality = DataQuality.Valid();
                var sources = metrics.GetPresentMetrics()
                    .Select(metric => new MetricSource(
                        metric,
                        provider,
                        batchId,
                        time,
                        quality.Status,
                        quality.Freshness));
                return new ForecastPoint(time, metrics, quality, sources);
            })
            .ToArray();

        return new ForecastBatch(
            batchId,
            dataDomain,
            provider,
            location,
            null,
            range.StartUtc.AddHours(-1),
            range.StartUtc.AddHours(-1),
            range,
            points,
            DataQuality.Valid());
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
