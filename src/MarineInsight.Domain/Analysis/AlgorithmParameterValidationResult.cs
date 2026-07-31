namespace MarineInsight.Domain.Analysis;

public sealed record AlgorithmParameterValidationResult
{
    public AlgorithmParameterValidationResult(IEnumerable<AlgorithmParameterValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public IReadOnlyList<AlgorithmParameterValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;

    public static AlgorithmParameterValidationResult Success() => new([]);
}
