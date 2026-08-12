namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class QueryHistoryEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? LocationId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public DateTimeOffset ForecastFromUtc { get; set; }

    public int Hours { get; set; }

    public string Activities { get; set; } = string.Empty;

    public Guid AnalysisId { get; set; }

    public string RiskLevel { get; set; } = string.Empty;

    public double? Score { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
