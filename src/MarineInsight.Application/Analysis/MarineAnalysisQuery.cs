using MarineInsight.Domain.Forecast;
using MarineInsight.Domain.Location;

namespace MarineInsight.Application.Analysis;

/// <summary>
/// The metrics-only query input used before deterministic scoring is introduced.
/// </summary>
public sealed record MarineAnalysisQuery
{
    public MarineAnalysisQuery(
        GeoPoint location,
        ForecastRange range,
        Location? locationMetadata = null)
    {
        if (locationMetadata is not null && locationMetadata.Coordinates != location)
        {
            throw new ArgumentException(
                "Location metadata coordinates must match the forecast query location.",
                nameof(locationMetadata));
        }

        Location = location;
        Range = range;
        LocationMetadata = locationMetadata;
    }

    public GeoPoint Location { get; }

    public ForecastRange Range { get; }

    public Location? LocationMetadata { get; }
}
