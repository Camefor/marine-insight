using MarineInsight.Application.Analysis;
using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Tests;

public sealed class AnalysisReportAssemblerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FromResultProjectsOverallFromEarliestAssessment()
    {
        var result = AnalysisTestFactory.CreateResult(
            [ActivityType.Boat],
            score: 72,
            riskLevel: RiskLevel.Good,
            confidence: 0.85);

        var report = AnalysisReportAssembler.FromResult(result, UserId, CreatedAtUtc);

        Assert.Equal(result.Snapshot.SnapshotId, report.Id);
        Assert.Equal(UserId, report.UserId);
        Assert.NotNull(report.Score);
        Assert.Equal(72d, report.Score.Value);
        Assert.Equal(RiskLevel.Good, report.RiskLevel);
        Assert.Equal(0.85, report.Confidence);
        Assert.Equal("marine-score-1.0.0", report.AlgorithmVersion);
        Assert.Equal("rule-template.v1", report.SummaryTemplateCode);
        Assert.Equal(ActivityType.Boat, report.ActivityType);
        Assert.Equal(CreatedAtUtc, report.CreatedAtUtc);
    }

    [Fact]
    public void FromResultUsesNullActivityTypeAndScoreForCompositeUnknownSet()
    {
        var result = AnalysisTestFactory.CreateResult(
            [],
            score: null,
            riskLevel: RiskLevel.Unknown,
            confidence: 0.4);

        var report = AnalysisReportAssembler.FromResult(result, UserId, CreatedAtUtc);

        Assert.Null(report.ActivityType);
        Assert.Null(report.Score);
        Assert.Equal(RiskLevel.Unknown, report.RiskLevel);
    }

    [Fact]
    public void FromResultKeepsOnlyNonInfoRisks()
    {
        var result = AnalysisTestFactory.CreateResult(
            [ActivityType.ShoreFishing],
            contributions:
            [
                AnalysisTestFactory.Risk("info-1", RiskSeverity.Info, 0.5),
                AnalysisTestFactory.Risk("warn-1", RiskSeverity.Warning, 20, actual: 5, threshold: 3),
                AnalysisTestFactory.Risk("danger-1", RiskSeverity.Danger, 40, actual: 9, threshold: 6)
            ]);

        var report = AnalysisReportAssembler.FromResult(result, UserId, CreatedAtUtc);

        Assert.Equal(2, report.Risks.Count);
        Assert.DoesNotContain(report.Risks, risk => risk.Severity == RiskSeverity.Info);
        Assert.Contains(report.Risks, risk => risk.RuleCode == "warn-1");
        Assert.Contains(report.Risks, risk => risk.RuleCode == "danger-1");
        Assert.Equal(40, report.Risks.Single(risk => risk.RuleCode == "danger-1").Penalty);
    }

    [Fact]
    public void FromResultSelectsBestScoringWindowAndMapsSources()
    {
        var windows = new[]
        {
            new RecommendationWindow(
                ActivityType.Boat,
                AnalysisTestFactory.StartUtc,
                AnalysisTestFactory.StartUtc.AddHours(4),
                null,
                null,
                null,
                60,
                40,
                4),
            new RecommendationWindow(
                ActivityType.Boat,
                AnalysisTestFactory.StartUtc.AddHours(6),
                AnalysisTestFactory.StartUtc.AddHours(10),
                AnalysisTestFactory.StartUtc.AddHours(9),
                AnalysisTestFactory.StartUtc.AddHours(10),
                "risky",
                90,
                70,
                4)
        };
        var result = AnalysisTestFactory.CreateResult([ActivityType.Boat], windows: windows);

        var report = AnalysisReportAssembler.FromResult(result, UserId, CreatedAtUtc);

        Assert.Equal(AnalysisTestFactory.StartUtc.AddHours(6), report.RecommendedStartUtc);
        Assert.Equal(AnalysisTestFactory.StartUtc.AddHours(10), report.RecommendedEndUtc);
        Assert.Equal(AnalysisTestFactory.StartUtc.AddHours(9), report.ReturnBeforeUtc);
        Assert.Equal(2, report.SourceBatches.Count);
        Assert.All(report.SourceBatches, source =>
        {
            Assert.Equal(AnalysisSourceRole.Primary, source.SourceRole);
            Assert.Equal("forecast-snapshot-assembler.v1", source.SelectionPolicy);
        });
        Assert.Equal(ForecastDataDomain.Weather, report.SourceBatches[0].DataDomain);
        Assert.Equal("weather-v1", report.SourceBatches[0].SourceModel);
        Assert.Equal(ForecastDataDomain.Marine, report.SourceBatches[1].DataDomain);
        Assert.Equal("marine-v1", report.SourceBatches[1].SourceModel);
    }
}
