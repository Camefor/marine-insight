namespace MarineInsight.Infrastructure.Persistence.Entities;

public sealed class FavoriteLocationEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid LocationId { get; set; }

    public string? DefaultActivity { get; set; }

    public string? Note { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public LocationEntity Location { get; set; } = null!;
}
