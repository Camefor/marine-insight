using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class ForecastBatchConfiguration : IEntityTypeConfiguration<ForecastBatchEntity>
{
    public void Configure(EntityTypeBuilder<ForecastBatchEntity> builder)
    {
        builder.ToTable("forecast_batches");
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.Id)
            .HasColumnName("id");
        builder.Property(batch => batch.LocationId)
            .HasColumnName("location_id")
            .IsRequired();

        builder.Property(batch => batch.ProviderCode)
            .HasColumnName("provider_code")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(batch => batch.DataDomain)
            .HasColumnName("data_domain")
            .IsRequired();
        builder.Property(batch => batch.EndpointCode)
            .HasColumnName("endpoint_code")
            .HasMaxLength(60);
        builder.Property(batch => batch.ModelCode)
            .HasColumnName("model_code")
            .HasMaxLength(40);
        builder.Property(batch => batch.IssuedAtUtc)
            .HasColumnName("issued_at");
        builder.Property(batch => batch.FetchedAtUtc)
            .HasColumnName("fetched_at")
            .IsRequired();
        builder.Property(batch => batch.RangeStartUtc)
            .HasColumnName("range_start")
            .IsRequired();
        builder.Property(batch => batch.RangeEndUtc)
            .HasColumnName("range_end")
            .IsRequired();
        builder.Property(batch => batch.QualityStatus)
            .HasColumnName("quality_status")
            .IsRequired();
        builder.Property(batch => batch.Freshness)
            .HasColumnName("freshness")
            .IsRequired();
        builder.Property(batch => batch.QualityFlags)
            .HasColumnName("quality_flags")
            .IsRequired();
        builder.Property(batch => batch.Completeness)
            .HasColumnName("completeness")
            .IsRequired();
        builder.Property(batch => batch.RequestedLatitude)
            .HasColumnName("requested_latitude")
            .HasPrecision(9, 6)
            .IsRequired();
        builder.Property(batch => batch.RequestedLongitude)
            .HasColumnName("requested_longitude")
            .HasPrecision(9, 6)
            .IsRequired();
        builder.Property(batch => batch.GridLatitude)
            .HasColumnName("grid_latitude")
            .HasPrecision(9, 6);
        builder.Property(batch => batch.GridLongitude)
            .HasColumnName("grid_longitude")
            .HasPrecision(9, 6);
        builder.Property(batch => batch.RawPayloadHash)
            .HasColumnName("raw_payload_hash")
            .HasMaxLength(64)
            .IsFixedLength();

        builder.HasOne(batch => batch.Location)
            .WithMany(location => location.ForecastBatches)
            .HasForeignKey(batch => batch.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(batch => batch.Points)
            .WithOne(point => point.Batch)
            .HasForeignKey(point => point.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(batch => new
        {
            batch.LocationId,
            batch.FetchedAtUtc
        });
        builder.HasIndex(batch => new
        {
            batch.ProviderCode,
            batch.DataDomain,
            batch.ModelCode,
            batch.LocationId,
            batch.IssuedAtUtc,
            batch.RangeStartUtc
        }).IsUnique();
    }
}
