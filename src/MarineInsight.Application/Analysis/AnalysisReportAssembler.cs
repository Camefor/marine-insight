using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Analysis;

/// <summary>
/// Projects a query result into the persisted <see cref="AnalysisReport"/> summary.
/// Keeps the row count bounded: only non-Info risks are stored, and only the
/// best-scoring recommendation window is retained.
/// </summary>
public static class AnalysisReportAssembler
{
    private const string SummaryTemplateCode = "rule-template.v1";

    public static AnalysisReport FromResult(
        MarineAnalysisQueryResult result,
        Guid userId,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        var rootAssessment = result.HourlyAssessments
            .OrderBy(assessment => assessment.ForecastTimeUtc)
            .FirstOrDefault();
        var bestWindow = result.RecommendedWindows
            .OrderByDescending(window => window.BestScore)
            .FirstOrDefault();

        var risks = result.HourlyAssessments
            .SelectMany(assessment => assessment.Contributions
                .Where(contribution => contribution.Severity != RiskSeverity.Info)
                .Select(contribution => new AnalysisRisk(
                    assessment.ForecastTimeUtc,
                    contribution.Code,
                    contribution.Severity,
                    contribution.Actual,
                    contribution.Threshold,
                    contribution.Penalty,
                    contribution.Message)))
            .ToArray();

        var sourceBatches = result.Snapshot.SourceBatches
            .Select(source => new AnalysisSourceBatch(
                source.BatchId,
                source.DataDomain,
                source.Provider.ProviderCode,
                source.Provider.SourceModel,
                ToSourceRole(source.DataDomain),
                result.CacheIdentity.SourceSelectionPolicy))
            .ToArray();

        return new AnalysisReport(
            result.Snapshot.SnapshotId,
            userId,
            result.Query.LocationMetadata?.Id,
            result.Snapshot.Range.StartUtc,
            result.Snapshot.Range.EndUtc,
            result.Snapshot.Range.Hours,
            result.CacheIdentity.AlgorithmVersion,
            result.CacheIdentity.SourceBatchSetHash,
            result.Query.Activities.Count == 1 ? result.Query.Activities[0] : null,
            rootAssessment?.Score,
            rootAssessment?.RiskLevel ?? RiskLevel.Unknown,
            rootAssessment?.Confidence ?? 0,
            bestWindow?.StartUtc,
            bestWindow?.EndUtc,
            bestWindow?.ReturnBeforeUtc,
            SummaryTemplateCode,
            createdAtUtc,
            risks,
            sourceBatches);
    }

    private static AnalysisSourceRole ToSourceRole(ForecastDataDomain dataDomain) =>
        dataDomain == ForecastDataDomain.Tide
            ? AnalysisSourceRole.Enhancement
            : AnalysisSourceRole.Primary;
}
