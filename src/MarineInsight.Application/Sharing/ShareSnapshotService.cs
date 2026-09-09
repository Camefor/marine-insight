using System.Security.Cryptography;
using MarineInsight.Application.Sharing.Ports;

namespace MarineInsight.Application.Sharing;

public sealed class ShareSnapshotService
{
    private const int MaximumPayloadLength = 2 * 1024 * 1024;

    private readonly IShareSnapshotRepository _repository;
    private readonly IShareSettingsRepository _settingsRepository;
    private readonly TimeProvider _timeProvider;

    public ShareSnapshotService(
        IShareSnapshotRepository repository,
        IShareSettingsRepository settingsRepository,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _settingsRepository = settingsRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ShareSnapshotCreateResult> CreateAsync(
        string payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Share payload is required.", nameof(payload));
        }

        if (payload.Length > MaximumPayloadLength)
        {
            throw new ArgumentException("Share payload cannot exceed 2 MB.", nameof(payload));
        }

        var now = _timeProvider.GetUtcNow();
        var settings = (await _settingsRepository.GetAsync(cancellationToken)).Normalize();
        var expiresAt = now.AddDays(settings.LinkValidityDays);
        var token = CreateToken();
        // 链接有效期由后台控制，但快照最多保留 30 天，便于过期清理且不延长公开访问权限。
        await _repository.SaveAsync(new ShareSnapshot(token, payload, now, expiresAt, now.AddDays(30)), cancellationToken);
        await _repository.DeleteExpiredAsync(now, cancellationToken);
        return new ShareSnapshotCreateResult(token, expiresAt);
    }

    public async Task<ShareSnapshot?> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        var snapshot = await _repository.GetActiveAsync(token, now, cancellationToken);
        await _repository.DeleteExpiredAsync(now, cancellationToken);
        return snapshot;
    }

    public Task<ShareSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        _settingsRepository.GetAsync(cancellationToken);

    public Task<ShareSettings> UpdateSettingsAsync(int linkValidityDays, CancellationToken cancellationToken = default)
    {
        if (linkValidityDays is < 1 or > ShareSettings.MaximumLinkValidityDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(linkValidityDays),
                linkValidityDays,
                $"Share link validity must be between 1 and {ShareSettings.MaximumLinkValidityDays} days.");
        }

        return _settingsRepository.SaveAsync(new ShareSettings(linkValidityDays), cancellationToken);
    }

    private static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[18];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
