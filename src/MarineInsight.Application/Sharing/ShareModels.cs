namespace MarineInsight.Application.Sharing;

public sealed record ShareSnapshot(
    string Token,
    string Payload,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset RetainUntilUtc);

public sealed record ShareSnapshotCreateResult(
    string Token,
    DateTimeOffset ExpiresAtUtc);

public sealed record ShareSettings(
    int LinkValidityDays)
{
    public const int DefaultLinkValidityDays = 7;
    public const int MaximumLinkValidityDays = 30;

    public static ShareSettings Default { get; } = new(DefaultLinkValidityDays);

    public ShareSettings Normalize()
    {
        return new ShareSettings(Math.Clamp(LinkValidityDays, 1, MaximumLinkValidityDays));
    }
}
