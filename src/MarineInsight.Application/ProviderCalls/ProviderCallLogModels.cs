namespace MarineInsight.Application.ProviderCalls;

public static class ProviderCallOperations
{
    public const string TideForecast = "tide.forecast";

    public const string CredentialValidation = "credential.validate";
}

public static class ProviderCallOutcomes
{
    public const string Started = "started";

    public const string Succeeded = "succeeded";

    public const string Failed = "failed";
}

public sealed record StartProviderCallLog(
    Guid ActorUserId,
    string ProviderCode,
    string Operation,
    Guid? CredentialId,
    string CredentialHint,
    double? LatitudeBucket,
    double? LongitudeBucket,
    DateTimeOffset? RangeStartUtc,
    DateTimeOffset? RangeEndUtc,
    int? RequestedDays,
    string? TraceId);

public sealed record CompleteProviderCallLog(
    bool Succeeded,
    int? HttpStatusCode,
    int? CreditsUsed,
    int? RemainingCredits,
    long DurationMs,
    string? ErrorCode);

public sealed record ProviderCallLogFilter(
    string? ProviderCode = null,
    string? Operation = null,
    string? Outcome = null,
    Guid? ActorUserId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Page = 1,
    int PageSize = 50);

public sealed record ProviderCallLogItem(
    Guid Id,
    Guid ActorUserId,
    string ProviderCode,
    string Operation,
    Guid? CredentialId,
    string CredentialHint,
    double? LatitudeBucket,
    double? LongitudeBucket,
    DateTimeOffset? RangeStartUtc,
    DateTimeOffset? RangeEndUtc,
    int? RequestedDays,
    string Outcome,
    int? HttpStatusCode,
    int? CreditsUsed,
    int? RemainingCredits,
    long? DurationMs,
    string? ErrorCode,
    string? TraceId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record ProviderCallLogPage(
    IReadOnlyList<ProviderCallLogItem> Items,
    int Total,
    int Page,
    int PageSize);
