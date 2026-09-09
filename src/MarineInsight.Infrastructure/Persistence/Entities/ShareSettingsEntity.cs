namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class ShareSettingsEntity
{
    public int Id { get; set; }

    public int LinkValidityDays { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
