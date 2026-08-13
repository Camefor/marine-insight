using MarineInsight.Application.Analysis;
using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Tests;

public sealed class ExplanationValidatorTests
{
    private const string ModelVersion = "gpt-4o-mini";

    [Fact]
    public void ValidCandidateReturnsAiExplanation()
    {
        var facts = CreateFacts();
        var candidate = new ExplanationCandidate
        {
            Headline = "整体海况良好，适宜乘船。",
            Summary = "综合评分约 72 分，风浪较小。",
            ActivityNotes = [new ExplanationActivityNote { Activity = "boat", Text = "可以安排乘船活动。" }]
        };

        var explanation = ExplanationValidator.TryValidate(candidate, facts, ModelVersion);

        Assert.NotNull(explanation);
        Assert.Equal(ExplanationSource.Ai, explanation.Source);
        Assert.False(explanation.Degraded);
        Assert.Equal(ModelVersion, explanation.ModelVersion);
        Assert.Equal(ExplanationDefaults.Locale, explanation.Locale);
        Assert.Equal(facts.Disclaimer, explanation.Disclaimer);
        Assert.Single(explanation.ActivityNotes);
    }

    [Fact]
    public void MissingHeadlineOrSummaryIsRejected()
    {
        var facts = CreateFacts();

        Assert.Null(ExplanationValidator.TryValidate(
            new ExplanationCandidate { Headline = "  ", Summary = "摘要" }, facts, ModelVersion));
        Assert.Null(ExplanationValidator.TryValidate(
            new ExplanationCandidate { Headline = "标题", Summary = " " }, facts, ModelVersion));
    }

    [Fact]
    public void ActivityOutsideRequestedSetIsRejected()
    {
        var facts = CreateFacts(supported: [ActivityType.Boat]);
        var candidate = ValidCandidate() with
        {
            ActivityNotes = [new ExplanationActivityNote { Activity = "shoreFishing", Text = "岸钓说明" }]
        };

        Assert.Null(ExplanationValidator.TryValidate(candidate, facts, ModelVersion));
    }

    [Fact]
    public void OptimisticTextIsRejectedWhenCautionIsRequired()
    {
        var facts = CreateFacts(riskLevel: RiskLevel.Caution);
        var candidate = ValidCandidate() with { Headline = "非常适宜，放心出海。" };

        Assert.Null(ExplanationValidator.TryValidate(candidate, facts, ModelVersion));
    }

    [Fact]
    public void NumericNotBackedByFactsIsRejected()
    {
        var facts = CreateFacts();
        var candidate = ValidCandidate() with { Summary = "综合评分约 99 分。" };

        Assert.Null(ExplanationValidator.TryValidate(candidate, facts, ModelVersion));
    }

    private static ExplanationCandidate ValidCandidate() => new()
    {
        Headline = "整体海况良好，适宜乘船。",
        Summary = "综合评分约 72 分，风浪较小。",
        ActivityNotes = [new ExplanationActivityNote { Activity = "boat", Text = "可以安排乘船活动。" }]
    };

    private static ExplanationFacts CreateFacts(
        RiskLevel riskLevel = RiskLevel.Good,
        IReadOnlyList<ActivityType>? supported = null) =>
        new(
            "东极岛",
            "Asia/Shanghai",
            AnalysisTestFactory.StartUtc,
            AnalysisTestFactory.StartUtc.AddHours(24),
            24,
            ForecastQualityStatus.Valid,
            ForecastFreshness.Fresh,
            1.0,
            [],
            new ExplanationOverallFact(72, riskLevel, 0.85, "marine-score-1.0.0"),
            [new ExplanationActivityFact(ActivityType.Boat, 72, riskLevel)],
            [],
            [],
            ExplanationDefaults.Disclaimer,
            supported ?? [ActivityType.Boat]);
}
