namespace MarineInsight.Domain.Analysis;

public sealed record RiskContribution
{
    public RiskContribution(
        string code,
        RiskContributionKind kind,
        RiskSeverity severity,
        string metric,
        double? actual,
        double? threshold,
        double penalty,
        string message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Risk contribution code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(metric))
        {
            throw new ArgumentException("Risk contribution metric is required.", nameof(metric));
        }

        if (penalty < 0 || !double.IsFinite(penalty))
        {
            throw new ArgumentOutOfRangeException(nameof(penalty), penalty, "Risk penalty must be finite and non-negative.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Risk contribution message is required.", nameof(message));
        }

        Code = code;
        Kind = kind;
        Severity = severity;
        Metric = metric;
        Actual = actual;
        Threshold = threshold;
        Penalty = penalty;
        Message = message;
    }

    public string Code { get; }

    public RiskContributionKind Kind { get; }

    public RiskSeverity Severity { get; }

    public string Metric { get; }

    public double? Actual { get; }

    public double? Threshold { get; }

    public double Penalty { get; }

    public string Message { get; }
}
