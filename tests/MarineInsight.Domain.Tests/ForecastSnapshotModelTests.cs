using MarineInsight.Domain.Forecast;

namespace MarineInsight.Domain.Tests;

public sealed class ForecastSnapshotModelTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);
    private static readonly ForecastRange Range = new(StartUtc, 24);
    private static readonly GeoPoint Location = new(30.194, 122.687);

    [Fact]
    public void SnapshotPointAllowsAlignedSourceToKeepItsActualForecastTime()
    {
        var provider = new ProviderIdentity("marine", "model-b");
        var batchId = Guid.NewGuid();
        var sourceTime = StartUtc.AddMinutes(15);
        var source = new MetricSource(
            ForecastMetricName.WaveHeightM,
            provider,
            batchId,
            sourceTime,
            ForecastQualityStatus.Valid,
            ForecastFreshness.Fresh);
        var point = new ForecastSnapshotPoint(
            StartUtc,
            ForecastMetricSet.Create(waveHeightM: 0.8),
            new SnapshotQuality(ForecastQualityStatus.Partial, ForecastFreshness.Fresh, 1, ForecastQualityMask.TimeGap),
            new[] { source });

        Assert.Equal(StartUtc, point.ForecastTimeUtc);
        Assert.Equal(sourceTime, point.MetricSources[0].ForecastTimeUtc);
    }

    [Fact]
    public void SnapshotRejectsMetricSourceThatIsNotListedInSourceBatches()
    {
        var provider = new ProviderIdentity("weather", "model-a");
        var sourceBatchId = Guid.NewGuid();
        var listedBatchId = Guid.NewGuid();
        var point = new ForecastSnapshotPoint(
            StartUtc,
            ForecastMetricSet.Create(windSpeedMs: 4),
            new SnapshotQuality(ForecastQualityStatus.Valid, ForecastFreshness.Fresh, 1),
            new[]
            {
                new MetricSource(
                    ForecastMetricName.WindSpeedMs,
                    provider,
                    sourceBatchId,
                    StartUtc,
                    ForecastQualityStatus.Valid,
                    ForecastFreshness.Fresh)
            });
        var reference = new SourceBatchReference(
            listedBatchId,
            ForecastDataDomain.Weather,
            provider,
            Location,
            null,
            StartUtc.AddHours(-1),
            StartUtc,
            Range,
            DataQuality.Valid());

        Assert.Throws<ArgumentException>(() => new ForecastSnapshot(
            Guid.NewGuid(),
            Location,
            Range,
            new[] { point },
            new[] { reference },
            new SnapshotQuality(ForecastQualityStatus.Valid, ForecastFreshness.Fresh, 1)));
    }
}
