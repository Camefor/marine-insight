namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class AuditLogEntity
{
    public Guid Id { get; set; }

    public Guid? ActorUserId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public string? TargetId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
