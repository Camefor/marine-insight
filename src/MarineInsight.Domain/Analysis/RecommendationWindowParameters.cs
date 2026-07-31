namespace MarineInsight.Domain.Analysis;

public sealed record RecommendationWindowParameters
{
    public RecommendationWindowParameters(
        int minimumWindowHours,
        double fastRiskScoreThreshold,
        double fastRiskDropThreshold,
        IReadOnlyDictionary<ActivityType, TimeSpan> returnBuffers)
    {
        ArgumentNullException.ThrowIfNull(returnBuffers);

        MinimumWindowHours = minimumWindowHours;
        FastRiskScoreThreshold = fastRiskScoreThreshold;
        FastRiskDropThreshold = fastRiskDropThreshold;
        ReturnBuffers = new Dictionary<ActivityType, TimeSpan>(returnBuffers);
    }

    public int MinimumWindowHours { get; }

    public double FastRiskScoreThreshold { get; }

    public double FastRiskDropThreshold { get; }

    public IReadOnlyDictionary<ActivityType, TimeSpan> ReturnBuffers { get; }
}
