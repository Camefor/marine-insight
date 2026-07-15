namespace MarineInsight.Application.Errors;

public class ProviderException : MarineInsightException
{
    public ProviderException(
        string providerCode,
        ProviderFailureKind failureKind,
        string message,
        bool isTransient,
        DateTimeOffset? retryAfterUtc = null,
        Exception? innerException = null)
        : base(GetErrorCode(failureKind), message, innerException)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
        {
            throw new ArgumentException("Provider code is required.", nameof(providerCode));
        }

        ProviderCode = providerCode.Trim().ToLowerInvariant();
        FailureKind = failureKind;
        IsTransient = isTransient;
        RetryAfterUtc = retryAfterUtc?.ToUniversalTime();
    }

    public string ProviderCode { get; }

    public ProviderFailureKind FailureKind { get; }

    public bool IsTransient { get; }

    public DateTimeOffset? RetryAfterUtc { get; }

    private static string GetErrorCode(ProviderFailureKind failureKind) => failureKind switch
    {
        ProviderFailureKind.RateLimited => MarineInsightErrorCodes.RateLimited,
        ProviderFailureKind.AuthenticationFailed => MarineInsightErrorCodes.ProviderAuthenticationFailed,
        ProviderFailureKind.ContractViolation => MarineInsightErrorCodes.ProviderContractInvalid,
        ProviderFailureKind.QuotaExceeded => MarineInsightErrorCodes.ProviderQuotaExceeded,
        _ => MarineInsightErrorCodes.ProviderUnavailable
    };
}

public sealed class ProviderTimeoutException : ProviderException
{
    public ProviderTimeoutException(
        string providerCode,
        string message,
        Exception? innerException = null)
        : base(providerCode, ProviderFailureKind.Timeout, message, true, innerException: innerException)
    {
    }
}

public sealed class ProviderRateLimitedException : ProviderException
{
    public ProviderRateLimitedException(
        string providerCode,
        string message,
        DateTimeOffset? retryAfterUtc = null,
        Exception? innerException = null)
        : base(providerCode, ProviderFailureKind.RateLimited, message, true, retryAfterUtc, innerException)
    {
    }
}

public sealed class ProviderAuthenticationException : ProviderException
{
    public ProviderAuthenticationException(
        string providerCode,
        string message,
        Exception? innerException = null)
        : base(providerCode, ProviderFailureKind.AuthenticationFailed, message, false, innerException: innerException)
    {
    }
}

public sealed class ProviderContractException : ProviderException
{
    public ProviderContractException(
        string providerCode,
        string message,
        Exception? innerException = null)
        : base(providerCode, ProviderFailureKind.ContractViolation, message, false, innerException: innerException)
    {
    }
}
