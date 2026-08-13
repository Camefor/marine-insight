namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class UserSettingEntity
{
    public Guid UserId { get; set; }

    public string WindSpeedUnit { get; set; } = "mps";

    public string WaveHeightUnit { get; set; } = "meter";

    public string TemperatureUnit { get; set; } = "celsius";

    public string? DefaultActivity { get; set; }

    public string? TimeZoneId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
