using MarineInsight.Domain.Analysis;

namespace MarineInsight.Application.Users;

public sealed record FavoriteLocation(
    Guid Id,
    Guid LocationId,
    string DisplayName,
    double Latitude,
    double Longitude,
    ActivityType? DefaultActivity,
    string? Note,
    int SortOrder,
    DateTimeOffset CreatedAtUtc);

public sealed record QueryHistoryItem(
    Guid Id,
    Guid? LocationId,
    string DisplayName,
    double Latitude,
    double Longitude,
    DateTimeOffset ForecastFromUtc,
    int Hours,
    IReadOnlyList<ActivityType> Activities,
    Guid AnalysisId,
    string RiskLevel,
    double? Score,
    DateTimeOffset CreatedAtUtc);

public sealed record UserLocation(
    Guid Id,
    string Name,
    double Latitude,
    double Longitude,
    ActivityType? DefaultActivity,
    string? Note,
    int SortOrder,
    DateTimeOffset CreatedAtUtc);

public sealed record UserSettings(
    string WindSpeedUnit,
    string WaveHeightUnit,
    string TemperatureUnit,
    ActivityType? DefaultActivity,
    string? TimeZoneId)
{
    public static UserSettings Default { get; } = new("mps", "meter", "celsius", null, null);
}

public sealed record SaveFavoriteCommand(
    Guid LocationId,
    ActivityType? DefaultActivity,
    string? Note,
    int SortOrder);

public sealed record SaveUserLocationCommand(
    string Name,
    double Latitude,
    double Longitude,
    ActivityType? DefaultActivity,
    string? Note,
    int SortOrder);

public sealed record RecordQueryHistoryCommand(
    Guid? LocationId,
    string DisplayName,
    double Latitude,
    double Longitude,
    DateTimeOffset ForecastFromUtc,
    int Hours,
    IReadOnlyList<ActivityType> Activities,
    Guid AnalysisId,
    string RiskLevel,
    double? Score);

public sealed class FavoriteAlreadyExistsException : Exception
{
    public FavoriteAlreadyExistsException(Guid locationId)
        : base($"Location '{locationId}' is already a favorite for this user.")
    {
    }
}
