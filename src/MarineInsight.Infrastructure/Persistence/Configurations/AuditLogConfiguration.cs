using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AuditLogEntity> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.EventType).HasMaxLength(100);
        builder.Property(entity => entity.TargetType).HasMaxLength(100);
        builder.Property(entity => entity.TargetId).HasMaxLength(200);
        builder.Property(entity => entity.Summary).HasMaxLength(1000);
        builder.HasIndex(entity => entity.CreatedAtUtc);
        builder.HasIndex(entity => new { entity.ActorUserId, entity.CreatedAtUtc });
    }
}
