using System.Security.Cryptography;
using MarineInsight.Application.Credentials;
using MarineInsight.Application.Credentials.Ports;
using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Persistence;

/// <summary>
/// Provider 密钥池存储：DataProtection 加密落库；管理面维护多候选密钥与激活项，Provider 面读取明文候选并回写健康/credits。
/// 密钥只在数据库中以密文形态存在，仓库内与项目目录内不出现明文。
/// </summary>
public sealed class ProviderCredentialStore : IProviderCredentialStore
{
    private const string Purpose = "MarineInsight.ProviderCredentials";

    private readonly MarineInsightDbContext _dbContext;
    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;

    public ProviderCredentialStore(
        MarineInsightDbContext dbContext,
        IDataProtectionProvider dataProtection,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _protector = (dataProtection ?? throw new ArgumentNullException(nameof(dataProtection))).CreateProtector(Purpose);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<IReadOnlyList<ProviderCredentialSummary>> ListAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.ProviderCredentials
            .AsNoTracking()
            .Where(credential => credential.ProviderName == providerName)
            .ToArrayAsync(cancellationToken);
        // SQLite 无法在 SQL 层对 DateTimeOffset 排序，密钥池行数很少，客户端排序足够。
        return entities
            .OrderBy(credential => credential.CreatedAtUtc)
            .ThenBy(credential => credential.Id)
            .Select(ToSummary)
            .ToArray();
    }

    public async Task AddAsync(
        Guid actorUserId,
        string providerName,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var isFirst = !await _dbContext.ProviderCredentials.AnyAsync(
            credential => credential.ProviderName == providerName,
            cancellationToken);
        var entity = new ProviderCredentialEntity
        {
            Id = Guid.NewGuid(),
            ProviderName = providerName,
            KeyHint = KeyHint(apiKey),
            EncryptedValue = _protector.Protect(apiKey),
            IsActive = isFirst,
            Health = nameof(ProviderCredentialHealth.Untested),
            CreditWarning = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            UpdatedByUserId = actorUserId
        };
        _dbContext.ProviderCredentials.Add(entity);
        _dbContext.AuditLogs.Add(CreateAudit(actorUserId, "provider.credential.added", entity, $"添加 {providerName} 密钥（末四位 {entity.KeyHint}）"));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(
        Guid actorUserId,
        string providerName,
        Guid keyId,
        CancellationToken cancellationToken = default)
    {
        var target = await _dbContext.ProviderCredentials.SingleOrDefaultAsync(
            credential => credential.Id == keyId && credential.ProviderName == providerName,
            cancellationToken);
        if (target is null)
        {
            throw new ArgumentException("Credential not found for the provider.", nameof(keyId));
        }

        if (target.IsActive)
        {
            return;
        }

        var others = await _dbContext.ProviderCredentials
            .Where(credential => credential.ProviderName == providerName && credential.IsActive)
            .ToListAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        foreach (var other in others)
        {
            other.IsActive = false;
            other.UpdatedAtUtc = now;
        }

        // SQLite 立即校验部分唯一索引（is_active=1 内 provider_name 唯一）；
        // 先持久化取消旧激活再激活目标，避免同一批次内瞬时出现两个激活行违反约束。
        await _dbContext.SaveChangesAsync(cancellationToken);

        target.IsActive = true;
        target.UpdatedAtUtc = now;
        target.UpdatedByUserId = actorUserId;
        _dbContext.AuditLogs.Add(CreateAudit(actorUserId, "provider.credential.activated", target, $"激活 {providerName} 密钥（末四位 {target.KeyHint}）"));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid actorUserId,
        string providerName,
        Guid keyId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ProviderCredentials.SingleOrDefaultAsync(
            credential => credential.Id == keyId && credential.ProviderName == providerName,
            cancellationToken);
        if (entity is null)
        {
            return;
        }

        // 激活密钥且仍存在其他密钥时拒绝，避免把整个 Provider 置为无可用 Key。
        if (entity.IsActive && await _dbContext.ProviderCredentials.AnyAsync(
            credential => credential.ProviderName == providerName && credential.Id != keyId,
            cancellationToken))
        {
            throw new ProviderCredentialInUseException("该密钥为当前启用项，请先激活其他密钥再删除。");
        }

        _dbContext.ProviderCredentials.Remove(entity);
        _dbContext.AuditLogs.Add(CreateAudit(actorUserId, "provider.credential.deleted", entity, $"删除 {providerName} 密钥（末四位 {entity.KeyHint}）"));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderCredentialSecret>> ListSecretsAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        var entities = (await _dbContext.ProviderCredentials
            .AsNoTracking()
            .Where(credential => credential.ProviderName == providerName)
            .ToArrayAsync(cancellationToken))
            // SQLite 无法在 SQL 层对 DateTimeOffset 排序；密钥池行数很少，客户端排序足够。
            .OrderByDescending(credential => credential.IsActive)
            .ThenBy(credential => credential.CreatedAtUtc)
            .ThenBy(credential => credential.Id)
            .ToArray();

        var secrets = new List<ProviderCredentialSecret>(entities.Length);
        foreach (var entity in entities)
        {
            // 解密失败（如 DataProtection 密钥卷丢失）时跳过该候选，让 Provider 回退到其余 Key/配置。
            string? apiKey;
            try
            {
                apiKey = _protector.Unprotect(entity.EncryptedValue);
            }
            catch (CryptographicException)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                secrets.Add(new ProviderCredentialSecret(entity.Id, apiKey, entity.IsActive));
            }
        }

        return secrets;
    }

    public async Task ReportHealthAsync(
        Guid? keyId,
        bool success,
        int? remainingCredits,
        bool creditWarning,
        string? failureReason,
        CancellationToken cancellationToken = default)
    {
        if (keyId is not { } id)
        {
            // 配置兜底密钥无 KeyId，不记健康。
            return;
        }

        var entity = await _dbContext.ProviderCredentials.SingleOrDefaultAsync(
            credential => credential.Id == id,
            cancellationToken);
        if (entity is null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        entity.Health = success ? nameof(ProviderCredentialHealth.Healthy) : nameof(ProviderCredentialHealth.Failed);
        entity.RemainingCredits = remainingCredits;
        entity.CreditWarning = creditWarning;
        entity.LastCheckedAtUtc = now;
        entity.LastFailureReason = success ? null : failureReason;
        entity.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ProviderCredentialSummary ToSummary(ProviderCredentialEntity entity) => new(
        entity.Id,
        entity.KeyHint,
        entity.IsActive,
        Enum.TryParse<ProviderCredentialHealth>(entity.Health, ignoreCase: true, out var health)
            ? health
            : ProviderCredentialHealth.Untested,
        entity.RemainingCredits,
        entity.CreditWarning,
        entity.LastCheckedAtUtc,
        entity.LastFailureReason,
        entity.CreatedAtUtc,
        entity.UpdatedAtUtc);

    private static string KeyHint(string apiKey) => $"••••{apiKey[^4..]}";

    private AuditLogEntity CreateAudit(
        Guid actorUserId,
        string eventType,
        ProviderCredentialEntity target,
        string summary) =>
        new()
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            EventType = eventType,
            TargetType = "ProviderCredential",
            TargetId = target.Id.ToString(),
            Summary = summary,
            CreatedAtUtc = _timeProvider.GetUtcNow()
        };
}
