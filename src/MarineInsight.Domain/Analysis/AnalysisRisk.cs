namespace MarineInsight.Domain.Analysis;

/// <summary>
/// A single notable risk factor captured in a persisted analysis result.
/// Only meaningful risks are stored; per-hour <see cref="RiskSeverity.Info"/> base
/// penalties are intentionally dropped to keep the row count bounded.
/// </summary>
public sealed record AnalysisRisk
{
    public AnalysisRisk(
        DateTimeOffset forecastTimeUtc,
        string ruleCode,
        RiskSeverity severity,
        double? actual,
        double? threshold,
        double penalty,
        string message)
    {
        if (string.IsNullOrWhiteSpace(ruleCode))
        {
            throw new ArgumentException("Risk rule code is required.", nameof(ruleCode));
        }

        if (penalty < 0 || !double.IsFinite(penalty))
        {
            throw new ArgumentOutOfRangeException(nameof(penalty), penalty, "Risk penalty must be finite and non-negative.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Risk message is required.", nameof(message));
        }

        ForecastTimeUtc = forecastTimeUtc.ToUniversalTime();
        RuleCode = ruleCode;
        Severity = severity;
        Actual = actual;
        Threshold = threshold;
        Penalty = penalty;
        Message = message;
    }

    public DateTimeOffset ForecastTimeUtc { get; }

    public string RuleCode { get; }

    public RiskSeverity Severity { get; }

    public double? Actual { get; }

    public double? Threshold { get; }

    public double Penalty { get; }

    public string Message { get; }
}
