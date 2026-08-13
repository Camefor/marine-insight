namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class AnalysisReportEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? LocationId { get; set; }

    public DateTimeOffset RangeStartUtc { get; set; }

    public DateTimeOffset RangeEndUtc { get; set; }

    public int Hours { get; set; }

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string SourceSetHash { get; set; } = string.Empty;

    public short? ActivityType { get; set; }

    public double? Score { get; set; }

    public short RiskLevel { get; set; }

    public double Confidence { get; set; }

    public DateTimeOffset? RecommendedStartUtc { get; set; }

    public DateTimeOffset? RecommendedEndUtc { get; set; }

    public DateTimeOffset? ReturnBeforeUtc { get; set; }

    public string SummaryTemplateCode { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ICollection<AnalysisRiskEntity> Risks { get; } = new List<AnalysisRiskEntity>();

    public ICollection<AnalysisSourceBatchEntity> SourceBatches { get; } = new List<AnalysisSourceBatchEntity>();
}
