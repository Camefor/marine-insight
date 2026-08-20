using MarineInsight.Application.Credentials;

namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class ProviderCredentialEntity
{
    public Guid Id { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    /// <summary>末 4 位掩码（••••abcd），仅用于界面辨识，不含明文。</summary>
    public string KeyHint { get; set; } = string.Empty;

    /// <summary>DataProtection 加密后的密钥。</summary>
    public string EncryptedValue { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string Health { get; set; } = nameof(ProviderCredentialHealth.Untested);

    public int? RemainingCredits { get; set; }

    public bool CreditWarning { get; set; }

    public DateTimeOffset? LastCheckedAtUtc { get; set; }

    public string? LastFailureReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }
}
