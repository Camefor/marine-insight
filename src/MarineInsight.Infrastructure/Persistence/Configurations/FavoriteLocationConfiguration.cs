using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class FavoriteLocationConfiguration : IEntityTypeConfiguration<FavoriteLocationEntity>
{
    public void Configure(EntityTypeBuilder<FavoriteLocationEntity> builder)
    {
        builder.ToTable("favorite_locations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.DefaultActivity).HasMaxLength(40);
        builder.Property(entity => entity.Note).HasMaxLength(500);
        builder.HasIndex(entity => new { entity.UserId, entity.LocationId }).IsUnique();
        builder.HasIndex(entity => new { entity.UserId, entity.SortOrder });
        builder.HasOne(entity => entity.Location)
            .WithMany()
            .HasForeignKey(entity => entity.LocationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MarineInsightUser>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
