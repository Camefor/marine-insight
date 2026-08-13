using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Persistence.Entities;

namespace MarineInsight.Infrastructure.Persistence;

internal static class AnalysisReportPersistenceMapper
{
    public static AnalysisReportEntity ToEntity(AnalysisReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var entity = new AnalysisReportEntity
        {
            Id = report.Id,
            UserId = report.UserId,
            LocationId = report.LocationId,
            RangeStartUtc = report.RangeStartUtc,
            RangeEndUtc = report.RangeEndUtc,
            Hours = report.Hours,
            AlgorithmVersion = report.AlgorithmVersion,
            SourceSetHash = report.SourceSetHash,
            ActivityType = report.ActivityType is { } activityType ? (short)activityType : null,
            Score = report.Score,
            RiskLevel = (short)report.RiskLevel,
            Confidence = report.Confidence,
            RecommendedStartUtc = report.RecommendedStartUtc,
            RecommendedEndUtc = report.RecommendedEndUtc,
            ReturnBeforeUtc = report.ReturnBeforeUtc,
            SummaryTemplateCode = report.SummaryTemplateCode,
            CreatedAtUtc = report.CreatedAtUtc
        };

        foreach (var risk in report.Risks)
        {
            entity.Risks.Add(new AnalysisRiskEntity
            {
                Id = Guid.NewGuid(),
                AnalysisResultId = report.Id,
                ForecastTimeUtc = risk.ForecastTimeUtc,
                RuleCode = risk.RuleCode,
                Severity = (short)risk.Severity,
                Actual = risk.Actual,
                Threshold = risk.Threshold,
                Penalty = risk.Penalty,
                Message = risk.Message
            });
        }

        foreach (var source in report.SourceBatches)
        {
            entity.SourceBatches.Add(new AnalysisSourceBatchEntity
            {
                AnalysisResultId = report.Id,
                BatchId = source.BatchId,
                SourceRole = (short)source.SourceRole,
                DataDomain = (short)source.DataDomain,
                ProviderCode = source.ProviderCode,
                SourceModel = source.SourceModel,
                SelectionPolicy = source.SelectionPolicy
            });
        }

        return entity;
    }

    public static AnalysisReport ToDomain(AnalysisReportEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var risks = entity.Risks
            .OrderBy(risk => risk.ForecastTimeUtc)
            .ThenBy(risk => risk.RuleCode)
            .Select(risk => new AnalysisRisk(
                risk.ForecastTimeUtc,
                risk.RuleCode,
                ToEnum<RiskSeverity>(risk.Severity),
                risk.Actual,
                risk.Threshold,
                risk.Penalty,
                risk.Message))
            .ToArray();

        var sourceBatches = entity.SourceBatches
            .OrderBy(source => source.DataDomain)
            .ThenBy(source => source.BatchId)
            .Select(source => new AnalysisSourceBatch(
                source.BatchId,
                ToEnum<ForecastDataDomain>(source.DataDomain),
                source.ProviderCode,
                source.SourceModel,
                ToEnum<AnalysisSourceRole>(source.SourceRole),
                source.SelectionPolicy))
            .ToArray();

        return new AnalysisReport(
            entity.Id,
            entity.UserId,
            entity.LocationId,
            entity.RangeStartUtc,
            entity.RangeEndUtc,
            entity.Hours,
            entity.AlgorithmVersion,
            entity.SourceSetHash,
            entity.ActivityType.HasValue ? ToEnum<ActivityType>(entity.ActivityType.Value) : null,
            entity.Score,
            ToEnum<RiskLevel>(entity.RiskLevel),
            entity.Confidence,
            entity.RecommendedStartUtc,
            entity.RecommendedEndUtc,
            entity.ReturnBeforeUtc,
            entity.SummaryTemplateCode,
            entity.CreatedAtUtc,
            risks,
            sourceBatches);
    }

    private static TEnum ToEnum<TEnum>(short value)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(typeof(TEnum), (int)value))
        {
            throw new InvalidOperationException($"Unknown {typeof(TEnum).Name} value '{value}'.");
        }

        return (TEnum)Enum.ToObject(typeof(TEnum), (int)value);
    }
}
