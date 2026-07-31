namespace MarineInsight.Domain.Analysis;

public sealed record RecommendationWindow
{
    public RecommendationWindow(
        ActivityType activityType,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        DateTimeOffset? returnBeforeUtc,
        DateTimeOffset? riskRisesAtUtc,
        string? riskReason,
        double bestScore,
        double minimumScore,
        int durationHours)
    {
        if (endUtc <= startUtc)
        {
            throw new ArgumentException("Recommendation window end must be later than start.", nameof(endUtc));
        }

        if (!double.IsFinite(bestScore) || bestScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(bestScore), bestScore, "Best score must be between 0 and 100.");
        }

        if (!double.IsFinite(minimumScore) || minimumScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumScore), minimumScore, "Minimum score must be between 0 and 100.");
        }

        if (durationHours <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationHours), durationHours, "Duration must be positive.");
        }

        ActivityType = activityType;
        StartUtc = startUtc.ToUniversalTime();
        EndUtc = endUtc.ToUniversalTime();
        ReturnBeforeUtc = returnBeforeUtc?.ToUniversalTime();
        RiskRisesAtUtc = riskRisesAtUtc?.ToUniversalTime();
        RiskReason = string.IsNullOrWhiteSpace(riskReason) ? null : riskReason;
        BestScore = bestScore;
        MinimumScore = minimumScore;
        DurationHours = durationHours;
    }

    public ActivityType ActivityType { get; }

    public DateTimeOffset StartUtc { get; }

    public DateTimeOffset EndUtc { get; }

    public DateTimeOffset? ReturnBeforeUtc { get; }

    public DateTimeOffset? RiskRisesAtUtc { get; }

    public string? RiskReason { get; }

    public double BestScore { get; }

    public double MinimumScore { get; }

    public int DurationHours { get; }
}
