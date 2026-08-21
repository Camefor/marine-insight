namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class ProviderCallLogEntity
{
    public Guid Id { get; set; }

    public Guid ActorUserId { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public Guid? CredentialId { get; set; }

    public string CredentialHint { get; set; } = string.Empty;

    public double? LatitudeBucket { get; set; }

    public double? LongitudeBucket { get; set; }

    public DateTimeOffset? RangeStartUtc { get; set; }

    public DateTimeOffset? RangeEndUtc { get; set; }

    public int? RequestedDays { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public int? HttpStatusCode { get; set; }

    public int? CreditsUsed { get; set; }

    public int? RemainingCredits { get; set; }

    public long? DurationMs { get; set; }

    public string? ErrorCode { get; set; }

    public string? TraceId { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
