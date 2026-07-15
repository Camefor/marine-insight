namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class ForecastBatchEntity
{
    public Guid Id { get; set; }

    public Guid LocationId { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public short DataDomain { get; set; }

    public string? EndpointCode { get; set; }

    public string? ModelCode { get; set; }

    public DateTimeOffset? IssuedAtUtc { get; set; }

    public DateTimeOffset FetchedAtUtc { get; set; }

    public DateTimeOffset RangeStartUtc { get; set; }

    public DateTimeOffset RangeEndUtc { get; set; }

    public short QualityStatus { get; set; }

    public short Freshness { get; set; }

    public int QualityFlags { get; set; }

    public double Completeness { get; set; }

    public decimal RequestedLatitude { get; set; }

    public decimal RequestedLongitude { get; set; }

    public decimal? GridLatitude { get; set; }

    public decimal? GridLongitude { get; set; }

    public string? RawPayloadHash { get; set; }

    public LocationEntity Location { get; set; } = null!;

    public ICollection<ForecastPointEntity> Points { get; } = new List<ForecastPointEntity>();
}
