using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Analysis;

/// <summary>
/// The metrics-only query input used before deterministic scoring is introduced.
/// </summary>
public sealed record MarineAnalysisQuery
{
    public MarineAnalysisQuery(GeoPoint location, ForecastRange range)
    {
        Location = location;
        Range = range;
    }

    public GeoPoint Location { get; }

    public ForecastRange Range { get; }
}
