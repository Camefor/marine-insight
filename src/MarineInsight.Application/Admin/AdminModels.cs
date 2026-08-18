using MarineInsight.Application.Errors;
using MarineInsight.Domain.Location;

namespace MarineInsight.Application.Admin;

public sealed record CreateLocationCommand(
    string DisplayName,
    double Latitude,
    double Longitude,
    string TimeZoneId,
    LocationType LocationType,
    double? CoastOrientationDeg);

public sealed record UpdateLocationCommand(
    string DisplayName,
    double Latitude,
    double Longitude,
    string TimeZoneId,
    LocationType LocationType,
    double? CoastOrientationDeg);

public sealed record AdminLocation(
    Guid Id,
    string DisplayName,
    LocationType LocationType,
    double Latitude,
    double Longitude,
    string TimeZoneId,
    double? CoastOrientationDeg,
    DateTimeOffset CreatedAtUtc);

public sealed record AdminUserSummary(
    Guid Id,
    string Email,
    bool EmailConfirmed,
    bool LockoutEnabled,
    DateTimeOffset? LockoutEnd,
    int AccessFailedCount);

/// <summary>预置地点删除结果，级联收藏数供前端确认。</summary>
public sealed record LocationDeleteResult(
    bool Deleted,
    int CascadedFavoriteCount);

/// <summary>创建/更新预置地点撞唯一索引（名称+坐标）时抛出。</summary>
public sealed class AdminLocationConflictException : MarineInsightException
{
    public AdminLocationConflictException(string message)
        : base(MarineInsightErrorCodes.LocationConflict, message)
    {
    }
}

/// <summary>删除预置地点时被预报批次引用而拒绝。</summary>
public sealed class AdminLocationInUseException : MarineInsightException
{
    public AdminLocationInUseException(string message)
        : base(MarineInsightErrorCodes.LocationInUse, message)
    {
    }
}
