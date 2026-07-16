using MarineInsight.Application.Forecast;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Analysis;

/// <summary>
/// Keeps source cache provenance next to the assembled snapshot for the HTTP projection.
/// </summary>
public sealed class MarineAnalysisQueryResult
{
    public MarineAnalysisQueryResult(
        MarineAnalysisQuery query,
        ForecastSnapshot snapshot,
        ForecastCacheResult weather,
        ForecastCacheResult marine)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(weather);
        ArgumentNullException.ThrowIfNull(marine);

        Query = query;
        Snapshot = snapshot;
        Weather = weather;
        Marine = marine;
    }

    public MarineAnalysisQuery Query { get; }

    public ForecastSnapshot Snapshot { get; }

    public ForecastCacheResult Weather { get; }

    public ForecastCacheResult Marine { get; }
}
