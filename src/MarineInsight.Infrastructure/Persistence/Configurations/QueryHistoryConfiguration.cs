using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class QueryHistoryConfiguration : IEntityTypeConfiguration<QueryHistoryEntity>
{
    public void Configure(EntityTypeBuilder<QueryHistoryEntity> builder)
    {
        builder.ToTable("query_history");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.DisplayName).HasMaxLength(200);
        builder.Property(entity => entity.Activities).HasMaxLength(300);
        builder.Property(entity => entity.RiskLevel).HasMaxLength(30);
        builder.HasIndex(entity => new { entity.UserId, entity.CreatedAtUtc });
        builder.HasOne<MarineInsightUser>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
