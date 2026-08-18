using MarineInsight.Domain.Location;

namespace MarineInsight.Application.Admin.Ports;

/// <summary>
/// Write boundary for the preset location catalog managed in the admin module.
/// </summary>
public interface IAdminLocationRepository
{
    Task<IReadOnlyList<Location>> ListPresetsAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByNormalizedCoordinatesAsync(
        string normalizedName,
        double latitude,
        double longitude,
        Guid excludeId,
        CancellationToken cancellationToken = default);

    /// <summary>统计以该预置地点为预设的收藏数，供删除前的级联确认。</summary>
    Task<int> CountFavoriteReferencesAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Location> AddAsync(
        Guid actorUserId,
        CreateLocationCommand command,
        CancellationToken cancellationToken = default);

    Task<Location?> UpdateAsync(
        Guid actorUserId,
        Guid id,
        UpdateLocationCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>返回 null 表示预置地点不存在。</summary>
    Task<LocationDeleteResult?> DeleteAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default);
}
