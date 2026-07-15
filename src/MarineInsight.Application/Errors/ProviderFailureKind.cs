namespace MarineInsight.Application.Errors;

public enum ProviderFailureKind
{
    Unavailable,
    Timeout,
    RateLimited,
    AuthenticationFailed,
    ContractViolation,
    QuotaExceeded
}
