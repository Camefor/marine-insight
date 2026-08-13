namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class AnalysisSourceBatchEntity
{
    public Guid AnalysisResultId { get; set; }

    public Guid BatchId { get; set; }

    public short SourceRole { get; set; }

    public short DataDomain { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public string SourceModel { get; set; } = string.Empty;

    public string SelectionPolicy { get; set; } = string.Empty;

    public AnalysisReportEntity AnalysisResult { get; set; } = null!;
}
