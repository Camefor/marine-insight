using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class ForecastPointConfiguration : IEntityTypeConfiguration<ForecastPointEntity>
{
    public void Configure(EntityTypeBuilder<ForecastPointEntity> builder)
    {
        builder.ToTable("forecast_points");
        builder.HasKey(point => point.Id);
        builder.Property(point => point.Id)
            .HasColumnName("id");

        builder.Property(point => point.BatchId)
            .HasColumnName("batch_id")
            .IsRequired();
        builder.Property(point => point.ForecastTimeUtc)
            .HasColumnName("forecast_time")
            .IsRequired();

        builder.Property(point => point.WindSpeedMs).HasColumnName("wind_speed_ms");
        builder.Property(point => point.WindGustMs).HasColumnName("wind_gust_ms");
        builder.Property(point => point.WindDirectionDeg).HasColumnName("wind_direction_deg");
        builder.Property(point => point.TemperatureC).HasColumnName("temperature_c");
        builder.Property(point => point.RelativeHumidityPct).HasColumnName("humidity_percent");
        builder.Property(point => point.SurfacePressureHpa).HasColumnName("pressure_hpa");
        builder.Property(point => point.CloudCoverPct).HasColumnName("cloud_cover_percent");
        builder.Property(point => point.PrecipitationMmPerHour).HasColumnName("precipitation_mm");
        builder.Property(point => point.CapeJkg).HasColumnName("cape_jkg");
        builder.Property(point => point.VisibilityM).HasColumnName("visibility_m");
        builder.Property(point => point.WeatherCode).HasColumnName("weather_code");
        builder.Property(point => point.Thunderstorm).HasColumnName("thunderstorm");
        builder.Property(point => point.WaveHeightM).HasColumnName("wave_height_m");
        builder.Property(point => point.WavePeriodS).HasColumnName("wave_period_s");
        builder.Property(point => point.WavePeakPeriodS).HasColumnName("wave_peak_period_s");
        builder.Property(point => point.WaveDirectionDeg).HasColumnName("wave_direction_deg");
        builder.Property(point => point.WindWaveHeightM).HasColumnName("wind_wave_height_m");
        builder.Property(point => point.WindWavePeriodS).HasColumnName("wind_wave_period_s");
        builder.Property(point => point.WindWavePeakPeriodS).HasColumnName("wind_wave_peak_period_s");
        builder.Property(point => point.WindWaveDirectionDeg).HasColumnName("wind_wave_direction_deg");
        builder.Property(point => point.SwellHeightM).HasColumnName("swell_height_m");
        builder.Property(point => point.SwellPeriodS).HasColumnName("swell_period_s");
        builder.Property(point => point.SwellPeakPeriodS).HasColumnName("swell_peak_period_s");
        builder.Property(point => point.SwellDirectionDeg).HasColumnName("swell_direction_deg");
        builder.Property(point => point.SeaTemperatureC).HasColumnName("sea_temperature_c");
        builder.Property(point => point.CurrentSpeedMs).HasColumnName("current_speed_ms");
        builder.Property(point => point.CurrentDirectionDeg).HasColumnName("current_direction_deg");
        builder.Property(point => point.TideHeightM).HasColumnName("tide_height_m");
        builder.Property(point => point.TideType).HasColumnName("tide_type");

        builder.Property(point => point.QualityStatus).HasColumnName("quality_status").IsRequired();
        builder.Property(point => point.Freshness).HasColumnName("freshness").IsRequired();
        builder.Property(point => point.MissingMetricsMask).HasColumnName("missing_mask").IsRequired();
        builder.Property(point => point.QualityFlags).HasColumnName("quality_flags").IsRequired();
        builder.Property(point => point.Completeness).HasColumnName("completeness").IsRequired();

        builder.HasIndex(point => new
        {
            point.BatchId,
            point.ForecastTimeUtc
        }).IsUnique();
        builder.HasMany(point => point.Sources)
            .WithOne(source => source.ForecastPoint)
            .HasForeignKey(source => source.ForecastPointId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
