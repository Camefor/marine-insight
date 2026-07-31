namespace MarineInsight.Domain.Analysis;

public sealed record CombinationRuleParameter
{
    public CombinationRuleParameter(
        string code,
        double penalty,
        IReadOnlyDictionary<string, double> thresholds,
        bool isEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        Code = code;
        Penalty = penalty;
        Thresholds = new Dictionary<string, double>(thresholds);
        IsEnabled = isEnabled;
    }

    public string Code { get; }

    public double Penalty { get; }

    public IReadOnlyDictionary<string, double> Thresholds { get; }

    public bool IsEnabled { get; }
}
