using MarineInsight.Application.Sharing;
using MarineInsight.Application.Sharing.Ports;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Persistence;

public sealed class ShareSnapshotRepository(
    MarineInsightDbContext dbContext,
    TimeProvider timeProvider) : IShareSnapshotRepository, IShareSettingsRepository
{
    public async Task SaveAsync(ShareSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        dbContext.ShareSnapshots.Add(new Entities.ShareSnapshotEntity
        {
            Token = snapshot.Token,
            Payload = snapshot.Payload,
            CreatedAtUtc = snapshot.CreatedAtUtc,
            ExpiresAtUtc = snapshot.ExpiresAtUtc,
            RetainUntilUtc = snapshot.RetainUntilUtc
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ShareSnapshot?> GetActiveAsync(
        string token,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ShareSnapshots
            .AsNoTracking()
            .SingleOrDefaultAsync(snapshot => snapshot.Token == token && snapshot.ExpiresAtUtc > nowUtc, cancellationToken);
        return entity is null
            ? null
            : new ShareSnapshot(entity.Token, entity.Payload, entity.CreatedAtUtc, entity.ExpiresAtUtc, entity.RetainUntilUtc);
    }

    public Task<int> DeleteExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
        // 使用固定保留截止时间清理，避免后台缩短链接有效期时误删仍在保留窗口内的快照。
        dbContext.ShareSnapshots
            .Where(snapshot => snapshot.RetainUntilUtc <= nowUtc)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<ShareSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ShareSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return entity is null
            ? ShareSettings.Default
            : new ShareSettings(entity.LinkValidityDays).Normalize();
    }

    public async Task<ShareSettings> SaveAsync(ShareSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = settings.Normalize();
        var entity = await dbContext.ShareSettings.SingleOrDefaultAsync(cancellationToken);
        if (entity is null)
        {
            entity = new Entities.ShareSettingsEntity { Id = 1 };
            dbContext.ShareSettings.Add(entity);
        }

        entity.LinkValidityDays = normalized.LinkValidityDays;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return normalized;
    }
}
