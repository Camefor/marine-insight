namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class AnalysisRiskEntity
{
    public Guid Id { get; set; }

    public Guid AnalysisResultId { get; set; }

    public DateTimeOffset ForecastTimeUtc { get; set; }

    public string RuleCode { get; set; } = string.Empty;

    public short Severity { get; set; }

    public double? Actual { get; set; }

    public double? Threshold { get; set; }

    public double Penalty { get; set; }

    public string Message { get; set; } = string.Empty;

    public AnalysisReportEntity AnalysisResult { get; set; } = null!;
}
