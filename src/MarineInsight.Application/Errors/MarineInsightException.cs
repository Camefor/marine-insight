namespace MarineInsight.Application.Errors;

public abstract class MarineInsightException : Exception
{
    protected MarineInsightException(
        string errorCode,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ArgumentException("Error code is required.", nameof(errorCode));
        }

        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
