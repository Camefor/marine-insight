namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class ShareSnapshotEntity
{
    public string Token { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset RetainUntilUtc { get; set; }
}
