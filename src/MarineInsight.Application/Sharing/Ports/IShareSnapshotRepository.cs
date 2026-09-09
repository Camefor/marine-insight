namespace MarineInsight.Application.Sharing.Ports;

public interface IShareSnapshotRepository
{
    Task SaveAsync(ShareSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<ShareSnapshot?> GetActiveAsync(string token, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}

public interface IShareSettingsRepository
{
    Task<ShareSettings> GetAsync(CancellationToken cancellationToken = default);

    Task<ShareSettings> SaveAsync(ShareSettings settings, CancellationToken cancellationToken = default);
}
