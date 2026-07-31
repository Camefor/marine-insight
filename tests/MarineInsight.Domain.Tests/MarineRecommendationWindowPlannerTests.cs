using MarineInsight.Domain.Analysis;

namespace MarineInsight.Domain.Tests;

public sealed class MarineRecommendationWindowPlannerTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 7, 31, 0, 0, 0, TimeSpan.Zero);
    private static readonly ActivityProfile BoatProfile = ActivityProfile.Defaults.Single(profile =>
        profile.ActivityType == ActivityType.Boat);

    [Fact]
    public void PlanCreatesWindowForConsecutiveRecommendedHours()
    {
        var assessments = new[]
        {
            CreateAssessment(0, 82),
            CreateAssessment(1, 84),
            CreateAssessment(2, 65, RiskLevel.Caution)
        };

        var window = Assert.Single(MarineRecommendationWindowPlanner.Plan(assessments, [BoatProfile]));

        Assert.Equal(ActivityType.Boat, window.ActivityType);
        Assert.Equal(StartUtc, window.StartUtc);
        Assert.Equal(StartUtc.AddHours(2), window.EndUtc);
        Assert.Equal(2, window.DurationHours);
        Assert.Equal(84, window.BestScore);
        Assert.Equal(BoatProfile.MinimumRecommendedScore, window.MinimumScore);
    }

    [Fact]
    public void PlanIgnoresSingleHourImprovement()
    {
        var assessments = new[]
        {
            CreateAssessment(0, 83),
            CreateAssessment(1, 62, RiskLevel.Caution),
            CreateAssessment(2, 84)
        };

        var windows = MarineRecommendationWindowPlanner.Plan(assessments, [BoatProfile]);

        Assert.Empty(windows);
    }

    [Fact]
    public void PlanSetsReturnBeforeWhenRiskRisesImmediatelyAfterWindow()
    {
        var assessments = new[]
        {
            CreateAssessment(0, 86),
            CreateAssessment(1, 82),
            CreateAssessment(2, 58, RiskLevel.Caution, CreateRisk("WIND_SPEED_BASE", RiskContributionKind.BasePenalty, 25, "平均风增强。"))
        };

        var window = Assert.Single(MarineRecommendationWindowPlanner.Plan(assessments, [BoatProfile]));

        Assert.Equal(StartUtc.AddHours(2), window.RiskRisesAtUtc);
        Assert.Equal(StartUtc.AddHours(1), window.ReturnBeforeUtc);
        Assert.Equal("平均风增强。", window.RiskReason);
    }

    [Fact]
    public void PlanExcludesUnknownAvoidAndSafetyGateHours()
    {
        var assessments = new[]
        {
            CreateAssessment(0, null, RiskLevel.Unknown),
            CreateAssessment(1, 48, RiskLevel.Avoid),
            CreateAssessment(2, 82, RiskLevel.Avoid, CreateRisk("WIND_SPEED_GATE", RiskContributionKind.SafetyGate, 100, "平均风速达到硬性高危阈值。"))
        };

        var windows = MarineRecommendationWindowPlanner.Plan(assessments, [BoatProfile]);

        Assert.Empty(windows);
    }

    private static HourlyMarineAssessment CreateAssessment(
        int hourOffset,
        double? activityScore,
        RiskLevel activityRiskLevel = RiskLevel.Good,
        RiskContribution? contribution = null,
        double confidence = 0.9)
    {
        var forecastTime = StartUtc.AddHours(hourOffset);
        var baseRiskLevel = activityRiskLevel == RiskLevel.Unknown
            ? RiskLevel.Unknown
            : contribution?.Kind == RiskContributionKind.SafetyGate
                ? RiskLevel.Avoid
                : activityRiskLevel;
        var baseScore = baseRiskLevel == RiskLevel.Unknown
            ? null
            : activityScore;
        var activity = new ActivityMarineAssessment(
            ActivityType.Boat,
            forecastTime,
            activityRiskLevel == RiskLevel.Unknown ? null : activityScore,
            activityRiskLevel,
            confidence,
            MarineRiskRuleEngine.DefaultAlgorithmVersion);

        return new HourlyMarineAssessment(
            forecastTime,
            baseScore,
            baseRiskLevel,
            confidence,
            MarineRiskRuleEngine.DefaultAlgorithmVersion,
            contribution is null ? [] : [contribution],
            [activity]);
    }

    private static RiskContribution CreateRisk(
        string code,
        RiskContributionKind kind,
        double penalty,
        string message) => new(
        code,
        kind,
        kind == RiskContributionKind.SafetyGate ? RiskSeverity.Blocking : RiskSeverity.Warning,
        "windSpeedMs",
        null,
        null,
        penalty,
        message);
}
