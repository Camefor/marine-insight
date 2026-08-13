using MarineInsight.Application.Analysis;
using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Tests;

public sealed class RuleTemplateExplanationGeneratorTests
{
    [Fact]
    public void GenerateProducesNonEmptyTemplate()
    {
        var facts = CreateFacts();

        var explanation = RuleTemplateExplanationGenerator.Generate(facts);

        Assert.Equal(ExplanationSource.Template, explanation.Source);
        Assert.False(explanation.Degraded);
        Assert.False(string.IsNullOrWhiteSpace(explanation.Headline));
        Assert.False(string.IsNullOrWhiteSpace(explanation.Summary));
        Assert.Equal(facts.Activities.Count, explanation.ActivityNotes.Count);
        Assert.Null(explanation.ModelVersion);
    }

    [Fact]
    public void CautionRiskLevelReflectedInHeadline()
    {
        var facts = CreateFacts(riskLevel: RiskLevel.Caution);

        var explanation = RuleTemplateExplanationGenerator.Generate(facts);

        Assert.Contains("谨慎", explanation.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public void ReturnBeforeWindowProducesReturnText()
    {
        var facts = CreateFacts(windows:
        [
            new ExplanationWindowFact(
                ActivityType.Boat,
                AnalysisTestFactory.StartUtc.AddHours(6),
                AnalysisTestFactory.StartUtc.AddHours(12),
                AnalysisTestFactory.StartUtc.AddHours(14),
                null,
                null)
        ]);

        var explanation = RuleTemplateExplanationGenerator.Generate(facts);

        Assert.NotNull(explanation.RiskWindowText);
        Assert.Contains("返航", explanation.RiskWindowText, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingMetricsProduceUncertaintyText()
    {
        var facts = CreateFacts(missingMetrics: ["wavePeriodS"]);

        var explanation = RuleTemplateExplanationGenerator.Generate(facts);

        Assert.NotNull(explanation.UncertaintyText);
        Assert.Contains("存在不确定性", explanation.UncertaintyText, StringComparison.Ordinal);
    }

    private static ExplanationFacts CreateFacts(
        RiskLevel riskLevel = RiskLevel.Good,
        IReadOnlyList<string>? missingMetrics = null,
        IReadOnlyList<ExplanationWindowFact>? windows = null) =>
        new(
            "东极岛",
            "Asia/Shanghai",
            AnalysisTestFactory.StartUtc,
            AnalysisTestFactory.StartUtc.AddHours(24),
            24,
            ForecastQualityStatus.Valid,
            ForecastFreshness.Fresh,
            1.0,
            missingMetrics ?? [],
            new ExplanationOverallFact(72, riskLevel, 0.85, "marine-score-1.0.0"),
            [new ExplanationActivityFact(ActivityType.Boat, 72, riskLevel)],
            [],
            windows ?? [],
            ExplanationDefaults.Disclaimer,
            [ActivityType.Boat]);
}
