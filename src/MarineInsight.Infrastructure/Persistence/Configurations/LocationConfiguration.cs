using MarineInsight.Domain.Location;
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
        builder.Property(location => location.IsHomeDefault)
            .HasColumnName("is_home_default")
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

        // Preset rows are deterministic catalog data and are applied only through migrations.
        builder.HasData(
            new LocationEntity
            {
                Id = new Guid("8a477d67-73fa-4f43-b954-cd29d238a89d"),
                NormalizedName = "东极岛",
                DisplayName = "东极岛",
                Latitude = 30.200m,
                Longitude = 122.680m,
                TimeZoneId = "Asia/Shanghai",
                LocationType = (short)LocationType.Island,
                IsPreset = true,
                CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new LocationEntity
            {
                Id = new Guid("70cfb8c4-7af7-4c43-8f38-9a27e7cc2de7"),
                NormalizedName = "嵊泗列岛",
                DisplayName = "嵊泗列岛",
                Latitude = 30.727m,
                Longitude = 122.451m,
                TimeZoneId = "Asia/Shanghai",
                LocationType = (short)LocationType.Island,
                IsPreset = true,
                CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new LocationEntity
            {
                Id = new Guid("d6ac8e90-44ae-4d1f-88b9-8b73db7af6a1"),
                NormalizedName = "普陀山",
                DisplayName = "普陀山",
                Latitude = 30.010m,
                Longitude = 122.388m,
                TimeZoneId = "Asia/Shanghai",
                LocationType = (short)LocationType.Island,
                IsPreset = true,
                CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new LocationEntity
            {
                Id = new Guid("9b2c4d6e-8f1a-4b7c-9d3e-5f0a2c4b6d8e"),
                NormalizedName = "岱山岛",
                DisplayName = "岱山岛",
                Latitude = 30.288m,
                Longitude = 122.165m,
                TimeZoneId = "Asia/Shanghai",
                LocationType = (short)LocationType.Island,
                IsPreset = true,
                CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            });
    }
}
