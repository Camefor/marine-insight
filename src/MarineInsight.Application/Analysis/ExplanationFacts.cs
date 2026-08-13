using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Analysis;

/// <summary>
/// Minimal facts derived from a deterministic analysis result. This is the only
/// input the rule template and the AI provider may read; it never carries raw
/// provider payloads, keys, identities or user text.
/// </summary>
public sealed record ExplanationFacts(
    string LocationName,
    string? TimeZoneId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int Hours,
    ForecastQualityStatus QualityStatus,
    ForecastFreshness Freshness,
    double Completeness,
    IReadOnlyList<string> MissingMetrics,
    ExplanationOverallFact? Overall,
    IReadOnlyList<ExplanationActivityFact> Activities,
    IReadOnlyList<ExplanationRiskFact> Risks,
    IReadOnlyList<ExplanationWindowFact> RecommendedWindows,
    string Disclaimer,
    IReadOnlyList<ActivityType> SupportedActivities);

public sealed record ExplanationOverallFact(
    double? Score,
    RiskLevel RiskLevel,
    double Confidence,
    string AlgorithmVersion);

public sealed record ExplanationActivityFact(
    ActivityType Activity,
    double? Score,
    RiskLevel RiskLevel);

public sealed record ExplanationRiskFact(
    string Code,
    RiskSeverity Severity,
    string Metric,
    double? Actual,
    double? Threshold,
    double Penalty,
    DateTimeOffset ForecastTimeUtc,
    string Message);

public sealed record ExplanationWindowFact(
    ActivityType Activity,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    DateTimeOffset? ReturnBeforeUtc,
    DateTimeOffset? RiskRisesAtUtc,
    string? RiskReason);
