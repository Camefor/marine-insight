namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class ForecastPointSourceEntity
{
    public Guid ForecastPointId { get; set; }

    public short Metric { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public string SourceModel { get; set; } = string.Empty;

    public Guid BatchId { get; set; }

    public DateTimeOffset ForecastTimeUtc { get; set; }

    public short QualityStatus { get; set; }

    public short Freshness { get; set; }

    public int QualityFlags { get; set; }

    public ForecastPointEntity ForecastPoint { get; set; } = null!;
}
