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
        IEnumerable<RecommendationWindow> recommendedWindows,
        MarineAnalysisCacheIdentity cacheIdentity,
        ForecastCacheResult weather,
        ForecastCacheResult marine,
        TideQueryStatus? tide = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(hourlyAssessments);
        ArgumentNullException.ThrowIfNull(recommendedWindows);
        ArgumentNullException.ThrowIfNull(cacheIdentity);
        ArgumentNullException.ThrowIfNull(weather);
        ArgumentNullException.ThrowIfNull(marine);

        Query = query;
        Snapshot = snapshot;
        HourlyAssessments = Array.AsReadOnly(hourlyAssessments.ToArray());
        RecommendedWindows = Array.AsReadOnly(recommendedWindows.ToArray());
        CacheIdentity = cacheIdentity;
        Weather = weather;
        Marine = marine;
        Tide = tide ?? TideQueryStatus.Disabled;
    }

    public MarineAnalysisQuery Query { get; }

    public ForecastSnapshot Snapshot { get; }

    public IReadOnlyList<HourlyMarineAssessment> HourlyAssessments { get; }

    public IReadOnlyList<RecommendationWindow> RecommendedWindows { get; }

    public MarineAnalysisCacheIdentity CacheIdentity { get; }

    public ForecastCacheResult Weather { get; }

    public ForecastCacheResult Marine { get; }

    public TideQueryStatus Tide { get; }
}

public sealed record TideQueryStatus(
    string Status,
    string CacheStatus,
    int? RemainingCredits,
    string? ErrorCode,
    ProviderTideResult? Result)
{
    public static TideQueryStatus Disabled { get; } = new("disabled", "none", null, null, null);
}
