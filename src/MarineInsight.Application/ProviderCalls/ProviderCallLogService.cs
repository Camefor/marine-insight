using MarineInsight.Application.ProviderCalls.Ports;

namespace MarineInsight.Application.ProviderCalls;

public sealed class ProviderCallLogService(IProviderCallLogStore store)
{
    private static readonly string[] KnownOutcomes =
    [
        ProviderCallOutcomes.Started,
        ProviderCallOutcomes.Succeeded,
        ProviderCallOutcomes.Failed
    ];

    public Task<ProviderCallLogPage> SearchAsync(
        ProviderCallLogFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), "Page must be at least 1.");
        }

        if (filter.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), "Page size must be between 1 and 100.");
        }

        if (filter.FromUtc.HasValue && filter.ToUtc.HasValue && filter.FromUtc > filter.ToUtc)
        {
            throw new ArgumentException("The start time cannot be later than the end time.", nameof(filter));
        }

        if (!string.IsNullOrWhiteSpace(filter.Outcome) &&
            !KnownOutcomes.Contains(filter.Outcome, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Unknown provider call outcome.", nameof(filter));
        }

        var normalized = filter with
        {
            ProviderCode = Normalize(filter.ProviderCode),
            Operation = Normalize(filter.Operation),
            Outcome = Normalize(filter.Outcome)
        };
        return store.SearchAsync(normalized, cancellationToken);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
