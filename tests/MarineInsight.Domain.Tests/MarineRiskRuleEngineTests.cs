using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Domain.Tests;

public sealed class MarineRiskRuleEngineTests
{
    private static readonly DateTimeOffset ForecastTimeUtc = new(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EvaluateCalmConditionsReturnsVeryGoodScore()
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 3,
            windGustMs: 5,
            waveHeightM: 0.3,
            wavePeriodS: 8,
            swellHeightM: 0.2,
            swellPeriodS: 8,
            visibilityM: 15_000,
            thunderstorm: false));

        Assert.Equal(RiskLevel.VeryGood, assessment.RiskLevel);
        Assert.Equal(97, assessment.Score);
        Assert.False(assessment.HasSafetyGate);
    }

    [Fact]
    public void EvaluateThunderstormTriggersSafetyGate()
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 3,
            windGustMs: 5,
            waveHeightM: 0.4,
            thunderstorm: true));

        Assert.Equal(RiskLevel.Avoid, assessment.RiskLevel);
        Assert.True(assessment.Score <= 49);
        Assert.Contains(assessment.Contributions, contribution => contribution.Code == "THUNDERSTORM_GATE");
    }

    [Theory]
    [InlineData(12.9, false)]
    [InlineData(13.0, true)]
    public void EvaluateWindSpeedGateUsesConfiguredBoundary(
        double windSpeedMs,
        bool shouldTriggerGate)
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: windSpeedMs,
            windGustMs: 8,
            waveHeightM: 0.6,
            thunderstorm: false));

        Assert.Equal(
            shouldTriggerGate,
            assessment.Contributions.Any(contribution => contribution.Code == "WIND_SPEED_GATE"));
    }

    [Theory]
    [InlineData(17.1, false)]
    [InlineData(17.2, true)]
    public void EvaluateWindGustGateUsesOfficialGaleBoundary(
        double windGustMs,
        bool shouldTriggerGate)
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 6,
            windGustMs: windGustMs,
            waveHeightM: 0.6,
            thunderstorm: false));

        // 17.2 m/s is the lower bound of Beaufort force 8 gale in domestic meteorological usage.
        Assert.Equal(
            shouldTriggerGate,
            assessment.Contributions.Any(contribution => contribution.Code == "WIND_GUST_GATE"));
    }

    [Theory]
    [InlineData(1.99, false)]
    [InlineData(2.0, true)]
    public void EvaluateWaveHeightGateUsesConfiguredBoundary(
        double waveHeightM,
        bool shouldTriggerGate)
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 4,
            windGustMs: 6,
            waveHeightM: waveHeightM,
            thunderstorm: false));

        Assert.Equal(
            shouldTriggerGate,
            assessment.Contributions.Any(contribution => contribution.Code == "WAVE_HEIGHT_GATE"));
    }

    [Theory]
    [InlineData(500, false)]
    [InlineData(499, true)]
    public void EvaluateLowVisibilityGateUsesStrictLessThanBoundary(
        double visibilityM,
        bool shouldTriggerGate)
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 3,
            windGustMs: 5,
            waveHeightM: 0.4,
            visibilityM: visibilityM,
            thunderstorm: false));

        Assert.Equal(
            shouldTriggerGate,
            assessment.Contributions.Any(contribution => contribution.Code == "VISIBILITY_LOW_GATE"));
    }

    [Fact]
    public void EvaluateLowWindHighWaveAddsCombinationRisk()
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 3,
            windGustMs: 5,
            waveHeightM: 2.5,
            swellHeightM: 2.0,
            thunderstorm: false));

        Assert.Equal(RiskLevel.Avoid, assessment.RiskLevel);
        Assert.Contains(assessment.Contributions, contribution => contribution.Code == "WIND_LOW_WAVE_HIGH");
        Assert.Contains(assessment.Contributions, contribution => contribution.Code == "WAVE_HEIGHT_GATE");
    }

    [Fact]
    public void EvaluateGustVolatilityAddsCombinationRisk()
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 4,
            windGustMs: 11,
            waveHeightM: 0.4,
            thunderstorm: false));

        Assert.Contains(assessment.Contributions, contribution => contribution.Code == "GUST_VOLATILITY");
        Assert.True(assessment.Score < 90);
    }

    [Fact]
    public void ActivityScoringAppliesProfileMultipliers()
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 4,
            windGustMs: 6,
            waveHeightM: 1.0,
            wavePeriodS: 8,
            swellHeightM: 0.2,
            visibilityM: 15_000,
            thunderstorm: false));
        var activityScores = MarineActivityScoringService.Evaluate(assessment, ActivityProfile.Defaults);

        var camping = activityScores.Single(activity => activity.ActivityType == ActivityType.Camping);
        var landing = activityScores.Single(activity => activity.ActivityType == ActivityType.Landing);

        Assert.True(camping.Score > landing.Score);
        Assert.Equal(RiskLevel.Good, camping.RiskLevel);
        Assert.Equal(RiskLevel.Avoid, landing.RiskLevel);
    }

    [Fact]
    public void ActivityScoringKeepsSafetyGateAsAvoidForEveryActivity()
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 3,
            windGustMs: 5,
            waveHeightM: 0.4,
            thunderstorm: true));
        var activityScores = MarineActivityScoringService.Evaluate(assessment, ActivityProfile.Defaults);

        Assert.All(activityScores, activity =>
        {
            Assert.Equal(RiskLevel.Avoid, activity.RiskLevel);
            Assert.True(activity.Score <= 49);
        });
    }

    [Fact]
    public void EvaluateShortPeriodWaveAddsSteepWaveRisk()
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 4,
            windGustMs: 6,
            waveHeightM: 0.8,
            wavePeriodS: 4,
            thunderstorm: false));

        Assert.Contains(assessment.Contributions, contribution => contribution.Code == "SHORT_STEEP_WAVE");
    }

    [Fact]
    public void EvaluateLongPeriodSwellAddsShoreRisk()
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 4,
            windGustMs: 6,
            waveHeightM: 0.8,
            swellHeightM: 0.9,
            swellPeriodS: 12,
            thunderstorm: false));

        Assert.Contains(assessment.Contributions, contribution => contribution.Code == "SWELL_LONG_PERIOD_SHORE");
    }

    [Fact]
    public void EvaluateMissingWaveAndSwellReturnsUnknownWithoutScore()
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 4,
            windGustMs: 6,
            visibilityM: 10_000,
            thunderstorm: false));

        Assert.Equal(RiskLevel.Unknown, assessment.RiskLevel);
        Assert.Null(assessment.Score);
        Assert.Contains(assessment.Contributions, contribution => contribution.Code == "DATA_INSUFFICIENT_MARINE");
    }

    private static HourlyMarineAssessment Evaluate(ForecastMetricSet metrics)
    {
        var point = CreatePoint(metrics);
        var engine = new MarineRiskRuleEngine();

        return engine.Evaluate(point);
    }

    private static ForecastSnapshotPoint CreatePoint(ForecastMetricSet metrics)
    {
        var provider = new ProviderIdentity("test", "model");
        var batchId = Guid.NewGuid();
        var sources = metrics.GetPresentMetrics()
            .Select(metric => new MetricSource(
                metric,
                provider,
                batchId,
                ForecastTimeUtc,
                ForecastQualityStatus.Valid,
                ForecastFreshness.Fresh))
            .ToArray();

        return new ForecastSnapshotPoint(
            ForecastTimeUtc,
            metrics,
            new SnapshotQuality(ForecastQualityStatus.Valid, ForecastFreshness.Fresh, 1),
            sources);
    }
}
