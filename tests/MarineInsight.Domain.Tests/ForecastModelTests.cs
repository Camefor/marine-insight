using MarineInsight.Domain.Forecast;

namespace MarineInsight.Domain.Tests;

public sealed class ForecastModelTests
{
    [Fact]
    public void GeoPointRejectsOutOfRangeLatitude()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPoint(91, 122));
    }

    [Fact]
    public void ForecastRangeNormalizesStartToUtc()
    {
        var start = new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.FromHours(8));

        var range = new ForecastRange(start, 24);

        Assert.Equal(start.ToUniversalTime(), range.StartUtc);
        Assert.Equal(start.ToUniversalTime().AddHours(24), range.EndUtc);
    }

    [Fact]
    public void ForecastMetricSetRejectsNonFiniteValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ForecastMetricSet.Create(windSpeedMs: double.NaN));
    }

    [Fact]
    public void ForecastPointRequiresASourceForEveryPresentMetric()
    {
        var metrics = ForecastMetricSet.Create(windSpeedMs: 3);

        Assert.Throws<ArgumentException>(() => new ForecastPoint(
            DateTimeOffset.UtcNow,
            metrics,
            DataQuality.Valid(),
            Array.Empty<MetricSource>()));
    }

    [Fact]
    public void ForecastBatchPreservesUtcAndSourceTraceability()
    {
        var batchId = Guid.NewGuid();
        var provider = new ProviderIdentity("Open-Meteo", "best-match");
        var start = new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.FromHours(8));
        var range = new ForecastRange(start, 24);
        var forecastTime = range.StartUtc;
        var point = new ForecastPoint(
            forecastTime,
            ForecastMetricSet.Create(windSpeedMs: 3),
            DataQuality.Valid(),
            new[]
            {
                new MetricSource(
                    ForecastMetricName.WindSpeedMs,
                    provider,
                    batchId,
                    forecastTime,
                    ForecastQualityStatus.Valid,
                    ForecastFreshness.Fresh)
            });

        var batch = new ForecastBatch(
            batchId,
            ForecastDataDomain.Weather,
            provider,
            new GeoPoint(30.194, 122.687),
            new GeoPoint(30.2, 122.7),
            start.AddHours(-1),
            start,
            range,
            new[] { point },
            DataQuality.Valid());

        Assert.Equal(ForecastDataDomain.Weather, batch.DataDomain);
        Assert.Equal(start.ToUniversalTime(), batch.IssuedAtUtc.AddHours(1));
        Assert.Equal(batchId, batch.Points[0].MetricSources[0].BatchId);
        Assert.Equal("open-meteo", batch.Provider.ProviderCode);
    }
}
