namespace MarineInsight.Domain.Analysis;

public sealed record ConfidenceParameters(
    double StaleFreshnessFactor,
    double ExpiredFreshnessFactor,
    double PartialStatusFactor,
    double StaleStatusFactor,
    double InvalidStatusFactor,
    double UnknownStatusFactor,
    double BlockingDataConfidenceCap,
    double MinimumRecommendationConfidence);
