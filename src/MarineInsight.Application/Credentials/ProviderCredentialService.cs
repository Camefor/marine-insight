using MarineInsight.Application.Credentials.Ports;

namespace MarineInsight.Application.Credentials;

/// <summary>
/// Provider 密钥池后台管理服务：校验输入后委托存储读写。
/// </summary>
public sealed class ProviderCredentialService
{
    private readonly IProviderCredentialStore _store;

    public ProviderCredentialService(IProviderCredentialStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<IReadOnlyList<ProviderCredentialSummary>> ListAsync(
        string providerName,
        CancellationToken cancellationToken = default) =>
        _store.ListAsync(providerName, cancellationToken);

    public async Task AddAsync(
        Guid actorUserId,
        string providerName,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        EnsureActor(actorUserId);
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name is required.", nameof(providerName));
        }

        await _store.AddAsync(actorUserId, providerName, ValidateApiKey(apiKey), cancellationToken);
    }

    public async Task SetActiveAsync(
        Guid actorUserId,
        string providerName,
        Guid keyId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor(actorUserId);
        if (keyId == Guid.Empty)
        {
            throw new ArgumentException("Key id cannot be empty.", nameof(keyId));
        }

        await _store.SetActiveAsync(actorUserId, providerName, keyId, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid actorUserId,
        string providerName,
        Guid keyId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor(actorUserId);
        if (keyId == Guid.Empty)
        {
            throw new ArgumentException("Key id cannot be empty.", nameof(keyId));
        }

        await _store.DeleteAsync(actorUserId, providerName, keyId, cancellationToken);
    }

    private static string ValidateApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required.", nameof(apiKey));
        }

        var trimmed = apiKey.Trim();
        if (trimmed.Length is < 8 or > 256)
        {
            throw new ArgumentException("API key must be between 8 and 256 characters.");
        }

        if (trimmed.Any(char.IsControl))
        {
            throw new ArgumentException("API key cannot contain control characters.");
        }

        return trimmed;
    }

    private static Guid EnsureActor(Guid actorUserId) => actorUserId != Guid.Empty
        ? actorUserId
        : throw new ArgumentException("A valid actor user id is required.", nameof(actorUserId));
}
