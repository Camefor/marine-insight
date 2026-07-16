using MarineInsight.Application.Forecast;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Tests;

public sealed class ForecastSnapshotAssemblerTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);
    private static readonly ForecastRange Range = new(StartUtc, 24);
    private static readonly GeoPoint Location = new(30.194, 122.687);

    [Fact]
    public void AssembleMergesWeatherAndMarineMetricsAndPreservesSources()
    {
        var weatherProvider = new ProviderIdentity("open-meteo", "weather-model");
        var marineProvider = new ProviderIdentity("open-meteo", "marine-model");
        var weather = CreateBatch(
            ForecastDataDomain.Weather,
            weatherProvider,
            (StartUtc, ForecastMetricSet.Create(windSpeedMs: 4, windGustMs: 6)));
        var marine = CreateBatch(
            ForecastDataDomain.Marine,
            marineProvider,
            (StartUtc, ForecastMetricSet.Create(waveHeightM: 0.8, swellHeightM: 0.5)));

        var snapshot = new ForecastSnapshotAssembler().Assemble(new[] { weather, marine }, Range);
        var point = Assert.Single(snapshot.Points);

        Assert.Equal(Location, snapshot.RequestedLocation);
        Assert.Equal(StartUtc, point.ForecastTimeUtc);
        Assert.Equal(4, point.Metrics.WindSpeedMs);
        Assert.Equal(0.8, point.Metrics.WaveHeightM);
        Assert.Equal(0.5, point.Metrics.SwellHeightM);
        Assert.Equal(ForecastQualityStatus.Valid, point.Quality.Status);
        Assert.Equal(ForecastQualityStatus.Valid, snapshot.Quality.Status);
        Assert.Equal(2, snapshot.SourceBatches.Count);
        Assert.Equal(4, point.MetricSources.Count);
        Assert.Contains(point.MetricSources, source => source.BatchId == weather.BatchId);
        Assert.Contains(point.MetricSources, source => source.BatchId == marine.BatchId);
    }

    [Fact]
    public void AssembleUsesNearestPointWithinLimitAndPreservesActualSourceTime()
    {
        var weatherProvider = new ProviderIdentity("weather", "model-a");
        var marineProvider = new ProviderIdentity("marine", "model-b");
        var marineTime = StartUtc.AddMinutes(15);
        var weather = CreateBatch(
            ForecastDataDomain.Weather,
            weatherProvider,
            (StartUtc, ForecastMetricSet.Create(windSpeedMs: 4)));
        var marine = CreateBatch(
            ForecastDataDomain.Marine,
            marineProvider,
            (marineTime, ForecastMetricSet.Create(waveHeightM: 0.8)));

        var snapshot = new ForecastSnapshotAssembler().Assemble(new[] { weather, marine }, Range);
        var firstPoint = snapshot.Points[0];
        var waveSource = firstPoint.MetricSources.Single(source => source.Metric == ForecastMetricName.WaveHeightM);

        Assert.Equal(2, snapshot.Points.Count);
        Assert.Equal(marineTime, waveSource.ForecastTimeUtc);
        Assert.Equal(ForecastQualityStatus.Partial, firstPoint.Quality.Status);
        Assert.Equal(ForecastFreshness.Fresh, firstPoint.Quality.Freshness);
        Assert.True(firstPoint.Quality.Flags.HasFlag(ForecastQualityMask.TimeGap));
        Assert.Equal(ForecastQualityStatus.Partial, snapshot.Quality.Status);
    }

    [Fact]
    public void AssembleMarksMissingDomainWhenNoPointIsWithinAlignmentLimit()
    {
        var weather = CreateBatch(
            ForecastDataDomain.Weather,
            new ProviderIdentity("weather", "model-a"),
            (StartUtc, ForecastMetricSet.Create(windSpeedMs: 4)));
        var marine = CreateBatch(
            ForecastDataDomain.Marine,
            new ProviderIdentity("marine", "model-b"),
            (StartUtc.AddHours(2), ForecastMetricSet.Create(waveHeightM: 0.8)));
        var options = new ForecastSnapshotAssemblyOptions
        {
            MaximumAlignmentGap = TimeSpan.FromMinutes(30)
        };

        var snapshot = new ForecastSnapshotAssembler().Assemble(new[] { weather, marine }, Range, options);

        Assert.Contains(ForecastDataDomain.Marine, snapshot.Points[0].Quality.MissingDomains);
        Assert.Contains(ForecastDataDomain.Weather, snapshot.Points[1].Quality.MissingDomains);
        Assert.All(snapshot.Points, point =>
        {
            Assert.Equal(ForecastQualityStatus.Partial, point.Quality.Status);
            Assert.True(point.Quality.Flags.HasFlag(ForecastQualityMask.TimeGap));
        });
    }

    [Fact]
    public void AssembleRequiresExplicitBatchSelectionForMultipleProvidersInOneDomain()
    {
        var providerA = new ProviderIdentity("provider-a", "model-a");
        var providerB = new ProviderIdentity("provider-b", "model-b");
        var batchA = CreateBatch(
            ForecastDataDomain.Weather,
            providerA,
            (StartUtc, ForecastMetricSet.Create(windSpeedMs: 3)));
        var batchB = CreateBatch(
            ForecastDataDomain.Weather,
            providerB,
            (StartUtc, ForecastMetricSet.Create(windSpeedMs: 8)));
        var assembler = new ForecastSnapshotAssembler();

        Assert.Throws<InvalidOperationException>(() => assembler.Assemble(new[] { batchA, batchB }, Range));

        var options = new ForecastSnapshotAssemblyOptions
        {
            PreferredBatchProviders = new Dictionary<ForecastDataDomain, ProviderIdentity>
            {
                [ForecastDataDomain.Weather] = providerB
            }
        };
        var snapshot = assembler.Assemble(new[] { batchA, batchB }, Range, options);

        Assert.Equal(8, snapshot.Points[0].Metrics.WindSpeedMs);
        Assert.Single(snapshot.SourceBatches);
        Assert.Equal(batchB.BatchId, snapshot.SourceBatches[0].BatchId);
    }

    [Fact]
    public void AssembleRequiresExplicitMetricSelectionForDuplicateMetrics()
    {
        var weatherProvider = new ProviderIdentity("weather", "model-a");
        var marineProvider = new ProviderIdentity("marine", "model-b");
        var weather = CreateBatch(
            ForecastDataDomain.Weather,
            weatherProvider,
            (StartUtc, ForecastMetricSet.Create(windSpeedMs: 3)));
        var marine = CreateBatch(
            ForecastDataDomain.Marine,
            marineProvider,
            (StartUtc, ForecastMetricSet.Create(windSpeedMs: 8, waveHeightM: 0.8)));
        var assembler = new ForecastSnapshotAssembler();

        Assert.Throws<InvalidOperationException>(() => assembler.Assemble(new[] { weather, marine }, Range));

        var options = new ForecastSnapshotAssemblyOptions
        {
            PreferredMetricProviders = new Dictionary<ForecastMetricName, ProviderIdentity>
            {
                [ForecastMetricName.WindSpeedMs] = marineProvider
            }
        };
        var snapshot = assembler.Assemble(new[] { weather, marine }, Range, options);
        var point = snapshot.Points[0];

        Assert.Equal(8, point.Metrics.WindSpeedMs);
        Assert.Equal(marineProvider, point.MetricSources.Single(source => source.Metric == ForecastMetricName.WindSpeedMs).Provider);
        Assert.Equal(0.8, point.Metrics.WaveHeightM);
    }

    [Fact]
    public void AssemblePropagatesStaleQualityWithoutMaskingIt()
    {
        var staleQuality = new DataQuality(
            ForecastQualityStatus.Stale,
            ForecastFreshness.Stale,
            1,
            ForecastQualityMask.StaleData);
        var weather = CreateBatch(
            ForecastDataDomain.Weather,
            new ProviderIdentity("weather", "model-a"),
            staleQuality,
            (StartUtc, ForecastMetricSet.Create(windSpeedMs: 4), staleQuality));

        var snapshot = new ForecastSnapshotAssembler().Assemble(new[] { weather }, Range);

        Assert.Equal(ForecastQualityStatus.Stale, snapshot.Points[0].Quality.Status);
        Assert.Equal(ForecastFreshness.Stale, snapshot.Points[0].Quality.Freshness);
        Assert.True(snapshot.Quality.Flags.HasFlag(ForecastQualityMask.StaleData));
    }

    private static ForecastBatch CreateBatch(
        ForecastDataDomain dataDomain,
        ProviderIdentity provider,
        params (DateTimeOffset Time, ForecastMetricSet Metrics, DataQuality? Quality)[] points)
    {
        return CreateBatch(dataDomain, provider, null, points);
    }

    private static ForecastBatch CreateBatch(
        ForecastDataDomain dataDomain,
        ProviderIdentity provider,
        DataQuality? batchQuality,
        params (DateTimeOffset Time, ForecastMetricSet Metrics, DataQuality? Quality)[] points)
    {
        return CreateBatchCore(dataDomain, provider, points, batchQuality);
    }

    private static ForecastBatch CreateBatch(
        ForecastDataDomain dataDomain,
        ProviderIdentity provider,
        params (DateTimeOffset Time, ForecastMetricSet Metrics)[] points)
    {
        var expandedPoints = points
            .Select(point => (point.Time, point.Metrics, (DataQuality?)null))
            .ToArray();
        return CreateBatchCore(dataDomain, provider, expandedPoints, null);
    }

    private static ForecastBatch CreateBatchCore(
        ForecastDataDomain dataDomain,
        ProviderIdentity provider,
        (DateTimeOffset Time, ForecastMetricSet Metrics, DataQuality? Quality)[] points,
        DataQuality? batchQuality)
    {
        var batchId = Guid.NewGuid();
        var forecastPoints = points
            .Select(item =>
            {
                var quality = item.Quality ?? DataQuality.Valid();
                var sources = item.Metrics.GetPresentMetrics()
                    .Select(metric => new MetricSource(
                        metric,
                        provider,
                        batchId,
                        item.Time,
                        quality.Status,
                        quality.Freshness,
                        quality.Flags));

                return new ForecastPoint(item.Time, item.Metrics, quality, sources);
            })
            .ToArray();

        return new ForecastBatch(
            batchId,
            dataDomain,
            provider,
            Location,
            null,
            Range.StartUtc.AddHours(-1),
            Range.StartUtc,
            Range,
            forecastPoints,
            batchQuality ?? DataQuality.Valid());
    }
}
