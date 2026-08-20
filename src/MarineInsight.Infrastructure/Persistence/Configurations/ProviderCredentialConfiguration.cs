using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class ProviderCredentialConfiguration : IEntityTypeConfiguration<ProviderCredentialEntity>
{
    public void Configure(EntityTypeBuilder<ProviderCredentialEntity> builder)
    {
        builder.ToTable("provider_credentials");
        builder.HasKey(credential => credential.Id);
        builder.Property(credential => credential.Id)
            .HasColumnName("id");

        builder.Property(credential => credential.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(credential => credential.KeyHint)
            .HasColumnName("key_hint")
            .HasMaxLength(8)
            .IsRequired();
        builder.Property(credential => credential.EncryptedValue)
            .HasColumnName("encrypted_value")
            .IsRequired();
        builder.Property(credential => credential.IsActive)
            .HasColumnName("is_active")
            .IsRequired();
        builder.Property(credential => credential.Health)
            .HasColumnName("health")
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(credential => credential.RemainingCredits)
            .HasColumnName("remaining_credits");
        builder.Property(credential => credential.CreditWarning)
            .HasColumnName("credit_warning")
            .IsRequired();
        builder.Property(credential => credential.LastCheckedAtUtc)
            .HasColumnName("last_checked_at");
        builder.Property(credential => credential.LastFailureReason)
            .HasColumnName("last_failure_reason")
            .HasMaxLength(200);
        builder.Property(credential => credential.CreatedAtUtc)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(credential => credential.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .IsRequired();
        builder.Property(credential => credential.UpdatedByUserId)
            .HasColumnName("updated_by_user_id");

        builder.HasIndex(credential => new { credential.ProviderName, credential.KeyHint })
            .IsUnique();

        // 每 Provider 至多一个激活密钥（SQLite/Postgres 均支持部分唯一索引）。
        builder.HasIndex(credential => credential.ProviderName)
            .HasFilter("\"is_active\"")
            .IsUnique();
    }
}
