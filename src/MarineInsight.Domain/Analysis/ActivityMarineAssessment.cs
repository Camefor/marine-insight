namespace MarineInsight.Domain.Analysis;

public sealed record ActivityMarineAssessment
{
    public ActivityMarineAssessment(
        ActivityType activityType,
        DateTimeOffset forecastTime,
        double? score,
        RiskLevel riskLevel,
        double confidence,
        string algorithmVersion)
    {
        if (score.HasValue && (!double.IsFinite(score.Value) || score.Value is < 0 or > 100))
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "Score must be between 0 and 100 or null.");
        }

        if (riskLevel == RiskLevel.Unknown && score.HasValue)
        {
            throw new ArgumentException("Unknown risk level cannot carry a numeric score.", nameof(score));
        }

        if (!double.IsFinite(confidence) || confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be between 0 and 1.");
        }

        if (string.IsNullOrWhiteSpace(algorithmVersion))
        {
            throw new ArgumentException("Algorithm version is required.", nameof(algorithmVersion));
        }

        ActivityType = activityType;
        ForecastTimeUtc = forecastTime.ToUniversalTime();
        Score = score;
        RiskLevel = riskLevel;
        Confidence = confidence;
        AlgorithmVersion = algorithmVersion;
    }

    public ActivityType ActivityType { get; }

    public DateTimeOffset ForecastTimeUtc { get; }

    public double? Score { get; }

    public RiskLevel RiskLevel { get; }

    public double Confidence { get; }

    public string AlgorithmVersion { get; }
}
