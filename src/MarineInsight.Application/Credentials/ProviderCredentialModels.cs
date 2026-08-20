using MarineInsight.Application.Errors;

namespace MarineInsight.Application.Credentials;

public enum ProviderCredentialHealth
{
    Untested,
    Healthy,
    Failed
}

/// <summary>密钥摘要（不含明文），供后台管理展示。</summary>
public sealed record ProviderCredentialSummary(
    Guid Id,
    string KeyHint,
    bool IsActive,
    ProviderCredentialHealth Health,
    int? RemainingCredits,
    bool CreditWarning,
    DateTimeOffset? LastCheckedAtUtc,
    string? LastFailureReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>明文密钥候选，仅供 Provider 请求期解析。</summary>
public sealed record ProviderCredentialSecret(
    Guid Id,
    string ApiKey,
    bool IsActive);

/// <summary>删除激活密钥且仍存在其他密钥时抛出，须先激活他者。</summary>
public sealed class ProviderCredentialInUseException : MarineInsightException
{
    public ProviderCredentialInUseException(string message)
        : base(MarineInsightErrorCodes.ProviderCredentialInUse, message)
    {
    }
}
