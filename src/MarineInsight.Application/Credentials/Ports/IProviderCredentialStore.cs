using MarineInsight.Application.Credentials;

namespace MarineInsight.Application.Credentials.Ports;

/// <summary>
/// Provider 密钥池存储：管理面维护多个候选密钥并选择激活项，Provider 面读取明文候选并回写健康/credits。
/// 密钥以 DataProtection 加密后持久化，仓库内与项目目录内均不出现明文。
/// </summary>
public interface IProviderCredentialStore
{
    /// <summary>列出某 Provider 的全部密钥摘要（不含明文），按创建时间排序。</summary>
    Task<IReadOnlyList<ProviderCredentialSummary>> ListAsync(string providerName, CancellationToken cancellationToken = default);

    /// <summary>新增密钥；若该 Provider 尚无任何密钥则自动激活为首个。</summary>
    Task AddAsync(Guid actorUserId, string providerName, string apiKey, CancellationToken cancellationToken = default);

    /// <summary>将指定密钥设为激活项，同 Provider 其他密钥去激活。</summary>
    Task SetActiveAsync(Guid actorUserId, string providerName, Guid keyId, CancellationToken cancellationToken = default);

    /// <summary>删除密钥；激活密钥且仍存在其他密钥时拒绝，须先激活他者。</summary>
    Task DeleteAsync(Guid actorUserId, string providerName, Guid keyId, CancellationToken cancellationToken = default);

    /// <summary>按请求优先级返回明文候选：激活项优先，其余按创建时间。</summary>
    Task<IReadOnlyList<ProviderCredentialSecret>> ListSecretsAsync(string providerName, CancellationToken cancellationToken = default);

    /// <summary>回写密钥健康与剩余 credits；keyId 为 null（配置兜底密钥）时跳过。</summary>
    Task ReportHealthAsync(
        Guid? keyId,
        bool success,
        int? remainingCredits,
        bool creditWarning,
        string? failureReason,
        CancellationToken cancellationToken = default);
}
