namespace MarineInsight.Domain.Forecast;

public sealed record DataQuality
{
    public DataQuality(
        ForecastQualityStatus status,
        ForecastFreshness freshness,
        double completeness,
        ForecastQualityMask flags = ForecastQualityMask.None,
        IEnumerable<ForecastMetricName>? missingMetrics = null)
    {
        if (!double.IsFinite(completeness) || completeness is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(completeness), completeness, "Completeness must be between 0 and 1.");
        }

        Status = status;
        Freshness = freshness;
        Completeness = completeness;
        Flags = flags;
        MissingMetrics = Array.AsReadOnly(
            (missingMetrics ?? Array.Empty<ForecastMetricName>()).Distinct().ToArray());
    }

    public ForecastQualityStatus Status { get; }

    public ForecastFreshness Freshness { get; }

    public double Completeness { get; }

    public ForecastQualityMask Flags { get; }

    public IReadOnlyList<ForecastMetricName> MissingMetrics { get; }

    public static DataQuality Valid() => new(
        ForecastQualityStatus.Valid,
        ForecastFreshness.Fresh,
        1);
}
