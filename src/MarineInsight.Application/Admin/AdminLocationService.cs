using MarineInsight.Application.Admin.Ports;
using MarineInsight.Domain.Location;

namespace MarineInsight.Application.Admin;

/// <summary>
/// 预置地点后台管理服务：校验输入、预检唯一冲突后委托仓储读写。
/// </summary>
public sealed class AdminLocationService
{
    private readonly IAdminLocationRepository _repository;

    public AdminLocationService(IAdminLocationRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<IReadOnlyList<Location>> ListPresetsAsync(CancellationToken cancellationToken = default) =>
        _repository.ListPresetsAsync(cancellationToken);

    public async Task<Location> CreateAsync(
        Guid actorUserId,
        CreateLocationCommand command,
        CancellationToken cancellationToken = default)
    {
        EnsureActor(actorUserId);
        Validate(command.DisplayName, command.Latitude, command.Longitude, command.TimeZoneId, command.LocationType, command.CoastOrientationDeg);
        await EnsureUniqueAsync(command.DisplayName, command.Latitude, command.Longitude, Guid.Empty, cancellationToken);
        return await _repository.AddAsync(actorUserId, command, cancellationToken);
    }

    public async Task<Location?> UpdateAsync(
        Guid actorUserId,
        Guid id,
        UpdateLocationCommand command,
        CancellationToken cancellationToken = default)
    {
        EnsureActor(actorUserId);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Location id cannot be empty.", nameof(id));
        }

        Validate(command.DisplayName, command.Latitude, command.Longitude, command.TimeZoneId, command.LocationType, command.CoastOrientationDeg);
        await EnsureUniqueAsync(command.DisplayName, command.Latitude, command.Longitude, id, cancellationToken);
        return await _repository.UpdateAsync(actorUserId, id, command, cancellationToken);
    }

    public Task<LocationDeleteResult?> DeleteAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureActor(actorUserId);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Location id cannot be empty.", nameof(id));
        }

        return _repository.DeleteAsync(actorUserId, id, cancellationToken);
    }

    public Task<int> CountFavoriteReferencesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Location id cannot be empty.", nameof(id));
        }

        return _repository.CountFavoriteReferencesAsync(id, cancellationToken);
    }

    private static void Validate(
        string displayName,
        double latitude,
        double longitude,
        string timeZoneId,
        LocationType locationType,
        double? coastOrientationDeg)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 200)
        {
            throw new ArgumentException("Display name is required and must not exceed 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Trim().Length > 64)
        {
            throw new ArgumentException("Time zone id is required and must not exceed 64 characters.");
        }

        if (!Enum.IsDefined(locationType) || locationType == LocationType.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(locationType), "Unknown or empty location type.");
        }

        // 坐标范围与海岸朝向范围复用域模型构造统一校验。
        _ = new Location(
            Guid.NewGuid(),
            displayName,
            displayName,
            latitude,
            longitude,
            timeZoneId,
            locationType,
            coastOrientationDeg,
            isPreset: true,
            DateTimeOffset.UtcNow);
    }

    private async Task EnsureUniqueAsync(
        string displayName,
        double latitude,
        double longitude,
        Guid excludeId,
        CancellationToken cancellationToken)
    {
        // NormalizedName 由显示名归一化，与域模型 Location 构造保持一致。
        var normalizedName = displayName.Trim().ToLowerInvariant();
        if (await _repository.ExistsByNormalizedCoordinatesAsync(normalizedName, latitude, longitude, excludeId, cancellationToken))
        {
            throw new AdminLocationConflictException("已存在同名同坐标的预置地点。");
        }
    }

    private static Guid EnsureActor(Guid actorUserId) => actorUserId != Guid.Empty
        ? actorUserId
        : throw new ArgumentException("A valid actor user id is required.", nameof(actorUserId));
}
