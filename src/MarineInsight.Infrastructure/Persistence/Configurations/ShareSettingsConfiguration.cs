using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class ShareSettingsConfiguration : IEntityTypeConfiguration<ShareSettingsEntity>
{
    public void Configure(EntityTypeBuilder<ShareSettingsEntity> builder)
    {
        builder.ToTable("share_settings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).HasColumnName("id");
        builder.Property(settings => settings.LinkValidityDays).HasColumnName("link_validity_days").IsRequired();
        builder.Property(settings => settings.UpdatedAtUtc).HasColumnName("updated_at").IsRequired();
    }
}
