using MarineInsight.Application.Admin;
using MarineInsight.Application.Admin.Ports;
using MarineInsight.Domain.Location;
using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Persistence;

/// <summary>
/// 预置地点后台写仓储：CRUD 同时落审计记录，删除前校验预报批次引用。
/// </summary>
public sealed class AdminLocationRepository(
    MarineInsightDbContext dbContext,
    TimeProvider timeProvider) : IAdminLocationRepository
{
    public async Task<IReadOnlyList<Location>> ListPresetsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.Locations
            .AsNoTracking()
            .Where(location => location.IsPreset)
            .OrderBy(location => location.DisplayName)
            .ThenBy(location => location.Id)
            .ToArrayAsync(cancellationToken);

        return entities.Select(ToDomain).ToArray();
    }

    public async Task<bool> ExistsByNormalizedCoordinatesAsync(
        string normalizedName,
        double latitude,
        double longitude,
        Guid excludeId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Locations.AnyAsync(
            location => location.NormalizedName == normalizedName
                && location.Latitude == (decimal)latitude
                && location.Longitude == (decimal)longitude
                && location.Id != excludeId,
            cancellationToken);
    }

    public Task<int> CountFavoriteReferencesAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.FavoriteLocations.CountAsync(
            favorite => favorite.LocationId == id,
            cancellationToken);

    public async Task<Location> AddAsync(
        Guid actorUserId,
        CreateLocationCommand command,
        CancellationToken cancellationToken = default)
    {
        var entity = new LocationEntity
        {
            Id = Guid.NewGuid(),
            NormalizedName = command.DisplayName.Trim().ToLowerInvariant(),
            DisplayName = command.DisplayName.Trim(),
            Latitude = (decimal)command.Latitude,
            Longitude = (decimal)command.Longitude,
            TimeZoneId = command.TimeZoneId.Trim(),
            LocationType = (short)command.LocationType,
            CoastOrientationDeg = command.CoastOrientationDeg is { } orientation ? (decimal)orientation : null,
            IsPreset = true,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        dbContext.Locations.Add(entity);
        dbContext.AuditLogs.Add(CreateAudit(actorUserId, "location.created", entity, $"创建预置地点 {entity.DisplayName}"));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDomain(entity);
    }

    public async Task<Location?> UpdateAsync(
        Guid actorUserId,
        Guid id,
        UpdateLocationCommand command,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Locations.SingleOrDefaultAsync(location => location.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.NormalizedName = command.DisplayName.Trim().ToLowerInvariant();
        entity.DisplayName = command.DisplayName.Trim();
        entity.Latitude = (decimal)command.Latitude;
        entity.Longitude = (decimal)command.Longitude;
        entity.TimeZoneId = command.TimeZoneId.Trim();
        entity.LocationType = (short)command.LocationType;
        entity.CoastOrientationDeg = command.CoastOrientationDeg is { } orientation ? (decimal)orientation : null;
        dbContext.AuditLogs.Add(CreateAudit(actorUserId, "location.updated", entity, $"更新预置地点 {entity.DisplayName}"));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDomain(entity);
    }

    public async Task<LocationDeleteResult?> DeleteAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Locations
            .AsNoTracking()
            .SingleOrDefaultAsync(location => location.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        // 被预报批次引用的预置地点不允许删除，否则触发数据库 Restrict 外键约束。
        if (await dbContext.ForecastBatches.AnyAsync(batch => batch.LocationId == id, cancellationToken))
        {
            throw new AdminLocationInUseException("该预置地点已被预报数据引用，无法删除。");
        }

        // favorite_locations 对预置地点为级联删除，删除前统计引用数供前端确认。
        var favoriteCount = await dbContext.FavoriteLocations.CountAsync(
            favorite => favorite.LocationId == id,
            cancellationToken);
        dbContext.Locations.Remove(new LocationEntity { Id = id });
        dbContext.AuditLogs.Add(CreateAudit(
            actorUserId,
            "location.deleted",
            entity,
            $"删除预置地点 {entity.DisplayName}" + (favoriteCount > 0 ? $"（级联删除 {favoriteCount} 条收藏）" : string.Empty)));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new LocationDeleteResult(Deleted: true, CascadedFavoriteCount: favoriteCount);
    }

    private AuditLogEntity CreateAudit(
        Guid actorUserId,
        string eventType,
        LocationEntity target,
        string summary) =>
        new()
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            EventType = eventType,
            TargetType = "Location",
            TargetId = target.Id.ToString(),
            Summary = summary,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

    private static Location ToDomain(LocationEntity entity) => new(
        entity.Id,
        entity.NormalizedName,
        entity.DisplayName,
        (double)entity.Latitude,
        (double)entity.Longitude,
        entity.TimeZoneId,
        (LocationType)entity.LocationType,
        entity.CoastOrientationDeg is { } orientation ? (double)orientation : null,
        entity.IsPreset,
        entity.CreatedAtUtc);
}
