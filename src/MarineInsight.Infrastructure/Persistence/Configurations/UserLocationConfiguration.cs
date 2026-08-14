using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class UserLocationConfiguration : IEntityTypeConfiguration<UserLocationEntity>
{
    public void Configure(EntityTypeBuilder<UserLocationEntity> builder)
    {
        builder.ToTable("user_locations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.DefaultActivity).HasMaxLength(40);
        builder.Property(entity => entity.Note).HasMaxLength(500);
        builder.HasIndex(entity => new { entity.UserId, entity.SortOrder });
        builder.HasOne<MarineInsightUser>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
