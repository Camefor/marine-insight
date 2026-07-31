using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;
using MarineInsight.Domain.Location;

namespace MarineInsight.Application.Analysis;

/// <summary>
/// Query input for forecast retrieval plus deterministic marine analysis.
/// </summary>
public sealed record MarineAnalysisQuery
{
    public MarineAnalysisQuery(
        GeoPoint location,
        ForecastRange range,
        Location? locationMetadata = null,
        IEnumerable<ActivityType>? activities = null)
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
        Activities = ActivityProfile.SelectDefaults(activities)
            .Select(profile => profile.ActivityType)
            .ToArray();
    }

    public GeoPoint Location { get; }

    public ForecastRange Range { get; }

    public Location? LocationMetadata { get; }

    public IReadOnlyList<ActivityType> Activities { get; }
}
