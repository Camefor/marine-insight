using System.Globalization;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast;

/// <summary>
/// Identifies one semantically complete normalized forecast cache entry.
/// </summary>
public readonly record struct ForecastCacheKey
{
    public ForecastCacheKey(
        string environment,
        ForecastDataDomain dataDomain,
        ProviderIdentity provider,
        GeoPoint location,
        ForecastRange range,
        string normalizerVersion,
        int coordinatePrecision)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrWhiteSpace(environment))
        {
            throw new ArgumentException("Cache environment is required.", nameof(environment));
        }

        if (!Enum.IsDefined(dataDomain))
        {
            throw new ArgumentOutOfRangeException(nameof(dataDomain), dataDomain, "Unknown forecast data domain.");
        }

        if (string.IsNullOrWhiteSpace(normalizerVersion))
        {
            throw new ArgumentException("Normalizer version is required.", nameof(normalizerVersion));
        }

        if (coordinatePrecision is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coordinatePrecision),
                coordinatePrecision,
                "Coordinate precision must be between 0 and 6 decimal places.");
        }

        EnsureUtcHourBoundary(range);

        Environment = NormalizeSegment(environment, nameof(environment));
        DataDomain = dataDomain;
        Provider = provider;
        CoordinatePrecision = coordinatePrecision;
        GridLocation = NormalizeLocation(location, coordinatePrecision);
        Range = range;
        NormalizerVersion = NormalizeSegment(normalizerVersion, nameof(normalizerVersion));
        Value = BuildValue();
    }

    public string Environment { get; }

    public ForecastDataDomain DataDomain { get; }

    public ProviderIdentity Provider { get; }

    /// <summary>
    /// The requested coordinate rounded to the configured cache grid precision.
    /// </summary>
    public GeoPoint GridLocation { get; }

    public ForecastRange Range { get; }

    public string NormalizerVersion { get; }

    public int CoordinatePrecision { get; }

    public string Value { get; }

    public static ForecastCacheKey Create(
        string environment,
        ForecastDataDomain dataDomain,
        ProviderIdentity provider,
        GeoPoint location,
        ForecastRange range,
        string normalizerVersion,
        int coordinatePrecision = 4) =>
        new(environment, dataDomain, provider, location, range, normalizerVersion, coordinatePrecision);

    public bool Matches(ForecastBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return batch.DataDomain == DataDomain
            // Some providers resolve a configured model such as "best_match" to a
            // concrete response model. The configured model remains the cache namespace;
            // the concrete response model is preserved in the batch for traceability.
            && string.Equals(batch.Provider.ProviderCode, Provider.ProviderCode, StringComparison.OrdinalIgnoreCase)
            && batch.Range == Range
            && NormalizeLocation(batch.RequestedLocation, CoordinatePrecision) == GridLocation;
    }

    private string BuildValue()
    {
        var latitude = FormatCoordinate(GridLocation.Latitude, CoordinatePrecision);
        var longitude = FormatCoordinate(GridLocation.Longitude, CoordinatePrecision);
        var start = Range.StartUtc.ToString("yyyyMMddHH", CultureInfo.InvariantCulture);

        return string.Join(
            ':',
            "mi",
            Environment,
            "forecast",
            DataDomain.ToString().ToLowerInvariant(),
            NormalizeSegment(Provider.ProviderCode, nameof(ProviderIdentity.ProviderCode)),
            NormalizeSegment(Provider.SourceModel, nameof(ProviderIdentity.SourceModel)),
            latitude,
            longitude,
            start,
            Range.Hours.ToString(CultureInfo.InvariantCulture),
            NormalizerVersion);
    }

    private static GeoPoint NormalizeLocation(GeoPoint location, int coordinatePrecision) =>
        new(
            NormalizeCoordinate(location.Latitude, coordinatePrecision),
            NormalizeCoordinate(location.Longitude, coordinatePrecision));

    private static double NormalizeCoordinate(double value, int coordinatePrecision)
    {
        var rounded = Math.Round(value, coordinatePrecision, MidpointRounding.AwayFromZero);
        return rounded == 0 ? 0 : rounded;
    }

    private static string FormatCoordinate(double value, int coordinatePrecision) =>
        NormalizeCoordinate(value, coordinatePrecision)
            .ToString($"F{coordinatePrecision}", CultureInfo.InvariantCulture);

    private static string NormalizeSegment(string value, string parameterName)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Cache key segments cannot be empty.", parameterName);
        }

        var normalized = Uri.EscapeDataString(trimmed.ToLowerInvariant());
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Cache key segments cannot be empty.", parameterName);
        }

        return normalized;
    }

    private static void EnsureUtcHourBoundary(ForecastRange range)
    {
        var start = range.StartUtc;
        if (start.Minute != 0 || start.Second != 0 || start.Millisecond != 0)
        {
            throw new ArgumentException(
                "Forecast cache ranges must start on a UTC hour boundary.",
                nameof(range));
        }
    }
}
