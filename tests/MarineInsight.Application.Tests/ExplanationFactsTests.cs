using MarineInsight.Application.Analysis;
using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Tests;

public sealed class ExplanationFactsTests
{
    [Fact]
    public void BuildProjectsRootAssessment()
    {
        var result = AnalysisTestFactory.CreateResult(
            [ActivityType.Boat],
            score: 72,
            riskLevel: RiskLevel.Good,
            confidence: 0.85);

        var facts = ExplanationFactsBuilder.Build(result);

        Assert.NotNull(facts.Overall);
        Assert.Equal(72, facts.Overall.Score);
        Assert.Equal(RiskLevel.Good, facts.Overall.RiskLevel);
        Assert.Equal(0.85, facts.Overall.Confidence);
        Assert.Equal(AnalysisTestFactory.AlgorithmVersion, facts.Overall.AlgorithmVersion);
        Assert.Single(facts.Activities);
        Assert.Equal(ActivityType.Boat, facts.Activities[0].Activity);
    }

    [Fact]
    public void BuildOrdersRisksBySeverityThenPenalty()
    {
        var result = AnalysisTestFactory.CreateResult(
            [ActivityType.Boat],
            contributions:
            [
                AnalysisTestFactory.Risk("WAVE", RiskSeverity.Warning, 10),
                AnalysisTestFactory.Risk("WIND", RiskSeverity.Danger, 20),
                AnalysisTestFactory.Risk("THUNDER", RiskSeverity.Danger, 5)
            ]);

        var facts = ExplanationFactsBuilder.Build(result);

        Assert.Equal(["WIND", "THUNDER", "WAVE"], facts.Risks.Select(risk => risk.Code).ToArray());
    }

    [Fact]
    public void BuildProjectsRecommendedWindows()
    {
        var window = new RecommendationWindow(
            ActivityType.Boat,
            AnalysisTestFactory.StartUtc.AddHours(6),
            AnalysisTestFactory.StartUtc.AddHours(12),
            AnalysisTestFactory.StartUtc.AddHours(14),
            AnalysisTestFactory.StartUtc.AddHours(15),
            "风浪增强",
            80,
            55,
            6);
        var result = AnalysisTestFactory.CreateResult([ActivityType.Boat], windows: [window]);

        var facts = ExplanationFactsBuilder.Build(result);

        var projected = Assert.Single(facts.RecommendedWindows);
        Assert.Equal(ActivityType.Boat, projected.Activity);
        Assert.Equal(window.StartUtc, projected.StartUtc);
        Assert.Equal(window.ReturnBeforeUtc, projected.ReturnBeforeUtc);
        Assert.Equal(window.RiskRisesAtUtc, projected.RiskRisesAtUtc);
        Assert.Equal("风浪增强", projected.RiskReason);
    }

    [Fact]
    public void BuildMapsMissingMetricsToCamelCase()
    {
        var result = AnalysisTestFactory.CreateResult(
            [ActivityType.Boat],
            snapshotQuality: new SnapshotQuality(
                ForecastQualityStatus.Partial,
                ForecastFreshness.Fresh,
                0.8,
                missingMetrics: [ForecastMetricName.WavePeriodS]));

        var facts = ExplanationFactsBuilder.Build(result);

        Assert.Contains("wavePeriodS", facts.MissingMetrics);
    }

    [Fact]
    public void BuildUsesCacheIdentityActivitiesAsSupported()
    {
        var result = AnalysisTestFactory.CreateResult([ActivityType.Boat, ActivityType.Camping]);

        var facts = ExplanationFactsBuilder.Build(result);

        Assert.Equal([ActivityType.Boat, ActivityType.Camping], facts.SupportedActivities);
    }
}
