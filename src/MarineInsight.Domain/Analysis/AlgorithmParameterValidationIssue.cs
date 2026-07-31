namespace MarineInsight.Domain.Analysis;

public sealed record AlgorithmParameterValidationIssue
{
    public AlgorithmParameterValidationIssue(
        string code,
        string message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Validation issue code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Validation issue message is required.", nameof(message));
        }

        Code = code;
        Message = message;
    }

    public string Code { get; }

    public string Message { get; }
}
