using MarineInsight.Domain.Forecast;

namespace MarineInsight.Domain.Analysis;

/// <summary>
/// Persisted summary of a single deterministic analysis run: the overall score and
/// risk level, the notable risks, the winning recommendation window, and the source
/// batches that produced it. Hour-by-hour series are intentionally not stored here;
/// they remain recoverable from the referenced forecast batches.
/// </summary>
public sealed record AnalysisReport
{
    public AnalysisReport(
        Guid id,
        Guid userId,
        Guid? locationId,
        DateTimeOffset rangeStartUtc,
        DateTimeOffset rangeEndUtc,
        int hours,
        string algorithmVersion,
        string sourceSetHash,
        ActivityType? activityType,
        double? score,
        RiskLevel riskLevel,
        double confidence,
        DateTimeOffset? recommendedStartUtc,
        DateTimeOffset? recommendedEndUtc,
        DateTimeOffset? returnBeforeUtc,
        string summaryTemplateCode,
        DateTimeOffset createdAtUtc,
        IEnumerable<AnalysisRisk> risks,
        IEnumerable<AnalysisSourceBatch> sourceBatches)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Analysis report ID is required.", nameof(id));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        if (rangeEndUtc <= rangeStartUtc)
        {
            throw new ArgumentException("Analysis range end must be later than start.", nameof(rangeEndUtc));
        }

        if (hours is not (24 or 72 or 168))
        {
            throw new ArgumentOutOfRangeException(nameof(hours), hours, "Analysis range must be 24, 72, or 168 hours.");
        }

        if (string.IsNullOrWhiteSpace(algorithmVersion))
        {
            throw new ArgumentException("Algorithm version is required.", nameof(algorithmVersion));
        }

        if (string.IsNullOrWhiteSpace(sourceSetHash))
        {
            throw new ArgumentException("Source set hash is required.", nameof(sourceSetHash));
        }

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

        if (recommendedStartUtc.HasValue != recommendedEndUtc.HasValue)
        {
            throw new ArgumentException("Recommended window start and end must be provided together.", nameof(recommendedStartUtc));
        }

        if (recommendedStartUtc is { } start && recommendedEndUtc is { } end && end <= start)
        {
            throw new ArgumentException("Recommended window end must be later than start.", nameof(recommendedEndUtc));
        }

        if (string.IsNullOrWhiteSpace(summaryTemplateCode))
        {
            throw new ArgumentException("Summary template code is required.", nameof(summaryTemplateCode));
        }

        ArgumentNullException.ThrowIfNull(risks);
        ArgumentNullException.ThrowIfNull(sourceBatches);

        Id = id;
        UserId = userId;
        LocationId = locationId;
        RangeStartUtc = rangeStartUtc.ToUniversalTime();
        RangeEndUtc = rangeEndUtc.ToUniversalTime();
        Hours = hours;
        AlgorithmVersion = algorithmVersion.Trim();
        SourceSetHash = sourceSetHash.Trim();
        ActivityType = activityType;
        Score = score;
        RiskLevel = riskLevel;
        Confidence = confidence;
        RecommendedStartUtc = recommendedStartUtc?.ToUniversalTime();
        RecommendedEndUtc = recommendedEndUtc?.ToUniversalTime();
        ReturnBeforeUtc = returnBeforeUtc?.ToUniversalTime();
        SummaryTemplateCode = summaryTemplateCode.Trim();
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        Risks = Array.AsReadOnly(risks.ToArray());
        SourceBatches = Array.AsReadOnly(sourceBatches.ToArray());
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public Guid? LocationId { get; }

    public DateTimeOffset RangeStartUtc { get; }

    public DateTimeOffset RangeEndUtc { get; }

    public int Hours { get; }

    public string AlgorithmVersion { get; }

    public string SourceSetHash { get; }

    public ActivityType? ActivityType { get; }

    public double? Score { get; }

    public RiskLevel RiskLevel { get; }

    public double Confidence { get; }

    public DateTimeOffset? RecommendedStartUtc { get; }

    public DateTimeOffset? RecommendedEndUtc { get; }

    public DateTimeOffset? ReturnBeforeUtc { get; }

    public string SummaryTemplateCode { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public IReadOnlyList<AnalysisRisk> Risks { get; }

    public IReadOnlyList<AnalysisSourceBatch> SourceBatches { get; }
}
