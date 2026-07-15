using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class LocationConfiguration : IEntityTypeConfiguration<LocationEntity>
{
    public void Configure(EntityTypeBuilder<LocationEntity> builder)
    {
        builder.ToTable("locations");
        builder.HasKey(location => location.Id);
        builder.Property(location => location.Id)
            .HasColumnName("id");

        builder.Property(location => location.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(location => location.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(location => location.Latitude)
            .HasColumnName("latitude")
            .HasPrecision(9, 6)
            .IsRequired();
        builder.Property(location => location.Longitude)
            .HasColumnName("longitude")
            .HasPrecision(9, 6)
            .IsRequired();
        builder.Property(location => location.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(location => location.LocationType)
            .HasColumnName("location_type")
            .IsRequired();
        builder.Property(location => location.CoastOrientationDeg)
            .HasColumnName("coast_orientation_deg")
            .HasPrecision(6, 2);
        builder.Property(location => location.IsPreset)
            .HasColumnName("is_preset")
            .IsRequired();
        builder.Property(location => location.CreatedAtUtc)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(location => new
        {
            location.NormalizedName,
            location.Latitude,
            location.Longitude
        }).IsUnique();
    }
}
