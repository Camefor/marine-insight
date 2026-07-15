namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class LocationEntity
{
    public Guid Id { get; set; }

    public string NormalizedName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public string TimeZoneId { get; set; } = string.Empty;

    public short LocationType { get; set; }

    public decimal? CoastOrientationDeg { get; set; }

    public bool IsPreset { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ICollection<ForecastBatchEntity> ForecastBatches { get; } = new List<ForecastBatchEntity>();
}
