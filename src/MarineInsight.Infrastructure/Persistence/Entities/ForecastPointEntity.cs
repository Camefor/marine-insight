namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class ForecastPointEntity
{
    public Guid Id { get; set; }

    public Guid BatchId { get; set; }

    public DateTimeOffset ForecastTimeUtc { get; set; }

    public double? WindSpeedMs { get; set; }

    public double? WindGustMs { get; set; }

    public double? WindDirectionDeg { get; set; }

    public double? TemperatureC { get; set; }

    public double? RelativeHumidityPct { get; set; }

    public double? SurfacePressureHpa { get; set; }

    public double? CloudCoverPct { get; set; }

    public double? PrecipitationMmPerHour { get; set; }

    public double? CapeJkg { get; set; }

    public double? VisibilityM { get; set; }

    public int? WeatherCode { get; set; }

    public bool? Thunderstorm { get; set; }

    public double? WaveHeightM { get; set; }

    public double? WavePeriodS { get; set; }

    public double? WavePeakPeriodS { get; set; }

    public double? WaveDirectionDeg { get; set; }

    public double? WindWaveHeightM { get; set; }

    public double? WindWavePeriodS { get; set; }

    public double? WindWavePeakPeriodS { get; set; }

    public double? WindWaveDirectionDeg { get; set; }

    public double? SwellHeightM { get; set; }

    public double? SwellPeriodS { get; set; }

    public double? SwellPeakPeriodS { get; set; }

    public double? SwellDirectionDeg { get; set; }

    public double? SeaTemperatureC { get; set; }

    public double? CurrentSpeedMs { get; set; }

    public double? CurrentDirectionDeg { get; set; }

    public double? TideHeightM { get; set; }

    public short? TideType { get; set; }

    public short QualityStatus { get; set; }

    public short Freshness { get; set; }

    public long MissingMetricsMask { get; set; }

    public int QualityFlags { get; set; }

    public double Completeness { get; set; }

    public ForecastBatchEntity Batch { get; set; } = null!;

    public ICollection<ForecastPointSourceEntity> Sources { get; } = new List<ForecastPointSourceEntity>();
}
