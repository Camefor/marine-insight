using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast;

public sealed record ForecastSnapshotAssemblyOptions
{
    public TimeSpan MaximumAlignmentGap { get; init; } = TimeSpan.FromMinutes(30);

    public IReadOnlyDictionary<ForecastDataDomain, ProviderIdentity> PreferredBatchProviders { get; init; } =
        new Dictionary<ForecastDataDomain, ProviderIdentity>();

    public IReadOnlyDictionary<ForecastMetricName, ProviderIdentity> PreferredMetricProviders { get; init; } =
        new Dictionary<ForecastMetricName, ProviderIdentity>();

    internal void Validate()
    {
        if (MaximumAlignmentGap < TimeSpan.Zero || MaximumAlignmentGap > TimeSpan.FromHours(6))
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumAlignmentGap),
                MaximumAlignmentGap,
                "Maximum alignment gap must be between 0 and 6 hours.");
        }

        ArgumentNullException.ThrowIfNull(PreferredBatchProviders);
        ArgumentNullException.ThrowIfNull(PreferredMetricProviders);

        foreach (var dataDomain in PreferredBatchProviders.Keys)
        {
            if (!Enum.IsDefined(dataDomain))
            {
                throw new ArgumentException("Preferred batch providers contain an unknown data domain.", nameof(PreferredBatchProviders));
            }
        }

        foreach (var metric in PreferredMetricProviders.Keys)
        {
            if (!Enum.IsDefined(metric))
            {
                throw new ArgumentException("Preferred metric providers contain an unknown metric.", nameof(PreferredMetricProviders));
            }
        }
    }
}
