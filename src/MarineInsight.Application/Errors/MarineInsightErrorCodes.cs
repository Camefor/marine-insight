namespace MarineInsight.Application.Errors;

public static class MarineInsightErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";

    public const string LocationNotFound = "LOCATION_NOT_FOUND";

    public const string ForecastInsufficient = "FORECAST_INSUFFICIENT";

    public const string ProviderUnavailable = "PROVIDER_UNAVAILABLE";

    public const string RateLimited = "RATE_LIMITED";

    public const string ProviderAuthenticationFailed = "PROVIDER_AUTHENTICATION_FAILED";

    public const string ProviderContractInvalid = "PROVIDER_CONTRACT_INVALID";

    public const string ProviderQuotaExceeded = "PROVIDER_QUOTA_EXCEEDED";

    public const string CacheUnavailable = "CACHE_UNAVAILABLE";

    public const string AiExplanationUnavailable = "AI_EXPLANATION_UNAVAILABLE";

    public const string AnalysisNotFound = "ANALYSIS_NOT_FOUND";

    public const string LocationConflict = "LOCATION_CONFLICT";

    public const string LocationInUse = "LOCATION_IN_USE";

    public const string ProviderCredentialInUse = "PROVIDER_CREDENTIAL_IN_USE";

    public const string ProviderCredentialConflict = "PROVIDER_CREDENTIAL_CONFLICT";
}
