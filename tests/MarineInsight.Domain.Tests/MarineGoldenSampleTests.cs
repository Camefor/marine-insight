using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Domain.Tests;

public sealed class MarineGoldenSampleTests
{
    private static readonly DateTimeOffset BaseForecastTimeUtc = new(2026, 7, 31, 15, 0, 0, TimeSpan.Zero);
    private static readonly ActivityProfile BoatProfile = ActivityProfile.Defaults.Single(profile =>
        profile.ActivityType == ActivityType.Boat);

    [Fact]
    public void GS001CalmSeaKeepsHighScoreWithoutSafetyGate()
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
        Assert.True(assessment.Score >= 90);
        Assert.False(assessment.HasSafetyGate);
        Assert.DoesNotContain(assessment.Contributions, contribution =>
            contribution.Kind == RiskContributionKind.SafetyGate);
    }

    [Fact]
    public void GS002LowWindHighWaveTriggersAvoidAndCombinationGate()
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 3,
            windGustMs: 5,
            waveHeightM: 2.5,
            swellHeightM: 2.0,
            thunderstorm: false));

        Assert.Equal(RiskLevel.Avoid, assessment.RiskLevel);
        Assert.True(assessment.HasSafetyGate);
        AssertContribution(assessment, "WIND_LOW_WAVE_HIGH");
        AssertContribution(assessment, "WAVE_HEIGHT_GATE");
    }

    [Fact]
    public void GS003GustSpikeReducesCampingAndBoatScores()
    {
        var assessment = EvaluateWithActivities(ForecastMetricSet.Create(
            windSpeedMs: 4,
            windGustMs: 11,
            waveHeightM: 0.4,
            swellHeightM: 0.2,
            visibilityM: 15_000,
            thunderstorm: false));

        var camping = GetActivity(assessment, ActivityType.Camping);
        var boat = GetActivity(assessment, ActivityType.Boat);

        AssertContribution(assessment, "GUST_VOLATILITY");
        Assert.Equal(RiskLevel.Caution, camping.RiskLevel);
        Assert.Equal(RiskLevel.Caution, boat.RiskLevel);
        Assert.True(camping.Score < 70);
        Assert.True(boat.Score < 70);
    }

    [Fact]
    public void GS004ShortPeriodWaveAddsBoatAndLandingRisk()
    {
        var assessment = EvaluateWithActivities(ForecastMetricSet.Create(
            windSpeedMs: 4,
            windGustMs: 6,
            waveHeightM: 0.8,
            wavePeriodS: 4,
            swellHeightM: 0.2,
            visibilityM: 15_000,
            thunderstorm: false));

        var boat = GetActivity(assessment, ActivityType.Boat);
        var landing = GetActivity(assessment, ActivityType.Landing);

        AssertContribution(assessment, "SHORT_STEEP_WAVE");
        Assert.Equal(RiskLevel.Caution, boat.RiskLevel);
        Assert.Equal(RiskLevel.Caution, landing.RiskLevel);
    }

    [Fact]
    public void GS005LongPeriodSwellPenalizesShoreAndLandingMoreThanBoat()
    {
        var assessment = EvaluateWithActivities(ForecastMetricSet.Create(
            windSpeedMs: 4,
            windGustMs: 6,
            waveHeightM: 0.8,
            wavePeriodS: 8,
            swellHeightM: 0.9,
            swellPeriodS: 12,
            visibilityM: 15_000,
            thunderstorm: false));

        var shoreFishing = GetActivity(assessment, ActivityType.ShoreFishing);
        var landing = GetActivity(assessment, ActivityType.Landing);
        var boat = GetActivity(assessment, ActivityType.Boat);

        AssertContribution(assessment, "SWELL_LONG_PERIOD_SHORE");
        Assert.Equal(RiskLevel.Avoid, shoreFishing.RiskLevel);
        Assert.Equal(RiskLevel.Avoid, landing.RiskLevel);
        Assert.True(shoreFishing.Score < boat.Score);
        Assert.True(landing.Score < boat.Score);
    }

    [Fact]
    public void GS006ThunderstormForcesEveryRelevantActivityToAvoid()
    {
        var assessment = EvaluateWithActivities(ForecastMetricSet.Create(
            windSpeedMs: 3,
            windGustMs: 5,
            waveHeightM: 0.4,
            swellHeightM: 0.2,
            thunderstorm: true));

        AssertContribution(assessment, "THUNDERSTORM_GATE");
        Assert.All(assessment.ActivityAssessments, activity =>
        {
            Assert.Equal(RiskLevel.Avoid, activity.RiskLevel);
            Assert.True(activity.Score <= 49);
        });
    }

    [Fact]
    public void GS007HighCapeWithoutThunderstormAddsAttentionPenaltyOnly()
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 3,
            windGustMs: 5,
            waveHeightM: 0.4,
            swellHeightM: 0.2,
            capeJkg: 1500,
            visibilityM: 15_000,
            thunderstorm: false));

        Assert.Equal(RiskLevel.Good, assessment.RiskLevel);
        Assert.False(assessment.HasSafetyGate);
        AssertContribution(assessment, "CAPE_BASE");
        Assert.DoesNotContain(assessment.Contributions, contribution =>
            contribution.Code == "THUNDERSTORM_GATE");
    }

    [Fact]
    public void GS008LowVisibilityTriggersSafetyGate()
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 3,
            windGustMs: 5,
            waveHeightM: 0.4,
            swellHeightM: 0.2,
            visibilityM: 400,
            thunderstorm: false));

        Assert.Equal(RiskLevel.Avoid, assessment.RiskLevel);
        Assert.True(assessment.HasSafetyGate);
        AssertContribution(assessment, "VISIBILITY_LOW_GATE");
    }

    [Fact]
    public void GS009MissingWaveAndSwellReturnsUnknownWithoutScore()
    {
        var assessment = Evaluate(ForecastMetricSet.Create(
            windSpeedMs: 4,
            windGustMs: 6,
            visibilityM: 10_000,
            thunderstorm: false));

        Assert.Equal(RiskLevel.Unknown, assessment.RiskLevel);
        Assert.Null(assessment.Score);
        AssertContribution(assessment, "DATA_INSUFFICIENT_MARINE");
    }

    [Fact]
    public void GS010RiskRisingAfterSeventeenEndsWindowWithReturnBuffer()
    {
        // 黄金样本用真实规则引擎先产出逐小时活动分，再验证窗口算法会在 17:00 风险上升前收口。
        var hourlyAssessments = new[]
        {
            EvaluateWithActivities(CreateGoodBoatWindowMetrics(), hourOffset: 0),
            EvaluateWithActivities(CreateGoodBoatWindowMetrics(), hourOffset: 1),
            EvaluateWithActivities(ForecastMetricSet.Create(
                windSpeedMs: 4,
                windGustMs: 7,
                waveHeightM: 2.1,
                swellHeightM: 0.4,
                visibilityM: 15_000,
                thunderstorm: false), hourOffset: 2)
        };

        var window = Assert.Single(MarineRecommendationWindowPlanner.Plan(hourlyAssessments, [BoatProfile]));

        Assert.Equal(ActivityType.Boat, window.ActivityType);
        Assert.Equal(BaseForecastTimeUtc, window.StartUtc);
        Assert.Equal(BaseForecastTimeUtc.AddHours(2), window.EndUtc);
        Assert.Equal(BaseForecastTimeUtc.AddHours(2), window.RiskRisesAtUtc);
        Assert.Equal(BaseForecastTimeUtc.AddHours(1), window.ReturnBeforeUtc);
        Assert.Contains("有效波高", window.RiskReason, StringComparison.Ordinal);
    }

    private static ForecastMetricSet CreateGoodBoatWindowMetrics() => ForecastMetricSet.Create(
        windSpeedMs: 3,
        windGustMs: 5,
        waveHeightM: 0.4,
        wavePeriodS: 8,
        swellHeightM: 0.2,
        swellPeriodS: 8,
        visibilityM: 15_000,
        thunderstorm: false);

    private static HourlyMarineAssessment Evaluate(ForecastMetricSet metrics, int hourOffset = 0)
    {
        var point = CreatePoint(metrics, BaseForecastTimeUtc.AddHours(hourOffset));
        var engine = new MarineRiskRuleEngine();

        return engine.Evaluate(point);
    }

    private static HourlyMarineAssessment EvaluateWithActivities(ForecastMetricSet metrics, int hourOffset = 0)
    {
        var assessment = Evaluate(metrics, hourOffset);
        var activityAssessments = MarineActivityScoringService.Evaluate(assessment, ActivityProfile.Defaults);

        return new HourlyMarineAssessment(
            assessment.ForecastTimeUtc,
            assessment.Score,
            assessment.RiskLevel,
            assessment.Confidence,
            assessment.AlgorithmVersion,
            assessment.Contributions,
            activityAssessments);
    }

    private static ForecastSnapshotPoint CreatePoint(
        ForecastMetricSet metrics,
        DateTimeOffset forecastTimeUtc)
    {
        var provider = new ProviderIdentity("golden-sample", "fixed");
        var batchId = Guid.NewGuid();
        var sources = metrics.GetPresentMetrics()
            .Select(metric => new MetricSource(
                metric,
                provider,
                batchId,
                forecastTimeUtc,
                ForecastQualityStatus.Valid,
                ForecastFreshness.Fresh))
            .ToArray();

        return new ForecastSnapshotPoint(
            forecastTimeUtc,
            metrics,
            new SnapshotQuality(ForecastQualityStatus.Valid, ForecastFreshness.Fresh, 1),
            sources);
    }

    private static ActivityMarineAssessment GetActivity(
        HourlyMarineAssessment assessment,
        ActivityType activityType) =>
        assessment.ActivityAssessments.Single(activity => activity.ActivityType == activityType);

    private static void AssertContribution(
        HourlyMarineAssessment assessment,
        string code) =>
        Assert.Contains(assessment.Contributions, contribution => contribution.Code == code);
}
