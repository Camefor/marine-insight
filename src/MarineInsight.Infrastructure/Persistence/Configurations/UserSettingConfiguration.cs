using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class UserSettingConfiguration : IEntityTypeConfiguration<UserSettingEntity>
{
    public void Configure(EntityTypeBuilder<UserSettingEntity> builder)
    {
        builder.ToTable("user_settings");
        builder.HasKey(entity => entity.UserId);
        builder.Property(entity => entity.WindSpeedUnit).HasMaxLength(20);
        builder.Property(entity => entity.WaveHeightUnit).HasMaxLength(20);
        builder.Property(entity => entity.TemperatureUnit).HasMaxLength(20);
        builder.Property(entity => entity.DefaultActivity).HasMaxLength(40);
        builder.Property(entity => entity.TimeZoneId).HasMaxLength(100);
        builder.HasOne<MarineInsightUser>()
            .WithOne()
            .HasForeignKey<UserSettingEntity>(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
