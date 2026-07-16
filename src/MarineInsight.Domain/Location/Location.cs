using MarineInsight.Domain.Forecast;

namespace MarineInsight.Domain.Location;

/// <summary>
/// A normalized location that can be used as a stable forecast query input.
/// </summary>
public sealed class Location
{
    public Location(
        Guid id,
        string normalizedName,
        string displayName,
        double latitude,
        double longitude,
        string timeZoneId,
        LocationType locationType,
        double? coastOrientationDeg,
        bool isPreset,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Location id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Normalized location name is required.", nameof(normalizedName));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Location display name is required.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new ArgumentException("Location time zone id is required.", nameof(timeZoneId));
        }

        if (!Enum.IsDefined(locationType))
        {
            throw new ArgumentOutOfRangeException(nameof(locationType), locationType, "Unknown location type.");
        }

        if (coastOrientationDeg is { } orientation &&
            (!double.IsFinite(orientation) || orientation is < 0 or >= 360))
        {
            throw new ArgumentOutOfRangeException(
                nameof(coastOrientationDeg),
                coastOrientationDeg,
                "Coast orientation must be in the range [0, 360) degrees.");
        }

        Id = id;
        NormalizedName = normalizedName.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
        Coordinates = new GeoPoint(latitude, longitude);
        TimeZoneId = timeZoneId.Trim();
        LocationType = locationType;
        CoastOrientationDeg = coastOrientationDeg;
        IsPreset = isPreset;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid Id { get; }

    public string NormalizedName { get; }

    public string DisplayName { get; }

    public GeoPoint Coordinates { get; }

    public GeoPoint Point => Coordinates;

    public double Latitude => Coordinates.Latitude;

    public double Longitude => Coordinates.Longitude;

    public string TimeZoneId { get; }

    public LocationType LocationType { get; }

    public double? CoastOrientationDeg { get; }

    public bool IsPreset { get; }

    public DateTimeOffset CreatedAtUtc { get; }
}
