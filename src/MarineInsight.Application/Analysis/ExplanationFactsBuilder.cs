using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Analysis;

public static class ExplanationFactsBuilder
{
    public static ExplanationFacts Build(
        MarineAnalysisQueryResult result,
        string? displayTimeZoneId = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var rootAssessment = result.HourlyAssessments
            .OrderBy(assessment => assessment.ForecastTimeUtc)
            .FirstOrDefault();

        var overall = rootAssessment is null
            ? null
            : new ExplanationOverallFact(
                rootAssessment.Score,
                rootAssessment.RiskLevel,
                rootAssessment.Confidence,
                rootAssessment.AlgorithmVersion);

        var activities = rootAssessment?.ActivityAssessments
            .Select(activity => new ExplanationActivityFact(
                activity.ActivityType,
                activity.Score,
                activity.RiskLevel))
            .ToArray() ?? [];

        var risks = result.HourlyAssessments
            .SelectMany(assessment => assessment.Contributions
                .Where(contribution => contribution.Penalty > 0)
                .Select(contribution => new ExplanationRiskFact(
                    contribution.Code,
                    contribution.Severity,
                    contribution.Metric,
                    contribution.Actual,
                    contribution.Threshold,
                    contribution.Penalty,
                    assessment.ForecastTimeUtc,
                    contribution.Message)))
            .OrderByDescending(risk => risk.Severity)
            .ThenByDescending(risk => risk.Penalty)
            .ThenBy(risk => risk.ForecastTimeUtc)
            .Take(8)
            .ToArray();

        var windows = result.RecommendedWindows
            .Select(window => new ExplanationWindowFact(
                window.ActivityType,
                window.StartUtc,
                window.EndUtc,
                window.ReturnBeforeUtc,
                window.RiskRisesAtUtc,
                window.RiskReason))
            .ToArray();

        var quality = result.Snapshot.Quality;

        // 展示与解读统一使用用户所在时区；未指定或无法解析时回退到地点的时区。
        var resolvedTimeZoneId = ResolveTimeZoneId(displayTimeZoneId, result.Query.LocationMetadata?.TimeZoneId);

        return new ExplanationFacts(
            result.Query.LocationMetadata?.DisplayName ?? "自定义坐标",
            resolvedTimeZoneId,
            result.Snapshot.Range.StartUtc,
            result.Snapshot.Range.EndUtc,
            result.Snapshot.Range.Hours,
            quality.Status,
            quality.Freshness,
            quality.Completeness,
            quality.MissingMetrics.Select(ToName).ToArray(),
            overall,
            activities,
            risks,
            windows,
            ExplanationDefaults.Disclaimer,
            result.CacheIdentity.Activities);
    }

    private static string? ResolveTimeZoneId(string? displayTimeZoneId, string? locationTimeZoneId)
    {
        var candidate = string.IsNullOrWhiteSpace(displayTimeZoneId)
            ? locationTimeZoneId
            : displayTimeZoneId;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(candidate);
            return candidate;
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        return locationTimeZoneId;
    }

    private static string ToName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
    }
}
