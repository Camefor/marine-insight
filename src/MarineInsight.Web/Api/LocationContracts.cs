namespace MarineInsight.Web.Api;

public sealed record LocationResponse(
    Guid Id,
    string DisplayName,
    string LocationType,
    double Latitude,
    double Longitude,
    string TimeZone,
    string Source);
