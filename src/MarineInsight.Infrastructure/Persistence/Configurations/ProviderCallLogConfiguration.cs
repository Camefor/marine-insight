using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class ProviderCallLogConfiguration : IEntityTypeConfiguration<ProviderCallLogEntity>
{
    public void Configure(EntityTypeBuilder<ProviderCallLogEntity> builder)
    {
        builder.ToTable("provider_call_logs");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ProviderCode).HasMaxLength(64);
        builder.Property(entity => entity.Operation).HasMaxLength(64);
        builder.Property(entity => entity.CredentialHint).HasMaxLength(16);
        builder.Property(entity => entity.Outcome).HasMaxLength(16);
        builder.Property(entity => entity.ErrorCode).HasMaxLength(100);
        builder.Property(entity => entity.TraceId).HasMaxLength(64);
        builder.HasIndex(entity => entity.StartedAtUtc);
        builder.HasIndex(entity => new { entity.ProviderCode, entity.StartedAtUtc });
        builder.HasIndex(entity => new { entity.ActorUserId, entity.StartedAtUtc });
        builder.HasIndex(entity => new { entity.Outcome, entity.StartedAtUtc });
    }
}
