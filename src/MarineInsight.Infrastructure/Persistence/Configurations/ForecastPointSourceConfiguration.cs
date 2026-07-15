using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class ForecastPointSourceConfiguration : IEntityTypeConfiguration<ForecastPointSourceEntity>
{
    public void Configure(EntityTypeBuilder<ForecastPointSourceEntity> builder)
    {
        builder.ToTable("forecast_point_sources");
        builder.HasKey(source => new { source.ForecastPointId, source.Metric });
        builder.Property(source => source.ForecastPointId)
            .HasColumnName("forecast_point_id")
            .IsRequired();

        builder.Property(source => source.Metric)
            .HasColumnName("metric")
            .IsRequired();
        builder.Property(source => source.ProviderCode)
            .HasColumnName("provider_code")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(source => source.SourceModel)
            .HasColumnName("source_model")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(source => source.BatchId)
            .HasColumnName("batch_id")
            .IsRequired();
        builder.Property(source => source.ForecastTimeUtc)
            .HasColumnName("forecast_time")
            .IsRequired();
        builder.Property(source => source.QualityStatus)
            .HasColumnName("quality_status")
            .IsRequired();
        builder.Property(source => source.Freshness)
            .HasColumnName("freshness")
            .IsRequired();
        builder.Property(source => source.QualityFlags)
            .HasColumnName("quality_flags")
            .IsRequired();
    }
}
