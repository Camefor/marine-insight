using MarineInsight.Application.Forecast;
using MarineInsight.Domain.Analysis;
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
        IEnumerable<HourlyMarineAssessment> hourlyAssessments,
        ForecastCacheResult weather,
        ForecastCacheResult marine)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(hourlyAssessments);
        ArgumentNullException.ThrowIfNull(weather);
        ArgumentNullException.ThrowIfNull(marine);

        Query = query;
        Snapshot = snapshot;
        HourlyAssessments = Array.AsReadOnly(hourlyAssessments.ToArray());
        Weather = weather;
        Marine = marine;
    }

    public MarineAnalysisQuery Query { get; }

    public ForecastSnapshot Snapshot { get; }

    public IReadOnlyList<HourlyMarineAssessment> HourlyAssessments { get; }

    public ForecastCacheResult Weather { get; }

    public ForecastCacheResult Marine { get; }
}
