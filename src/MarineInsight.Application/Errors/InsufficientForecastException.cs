using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Errors;

public sealed class InsufficientForecastException : MarineInsightException
{
    public InsufficientForecastException(IEnumerable<ForecastMetricName> missingMetrics)
        : base(
            MarineInsightErrorCodes.ForecastInsufficient,
            "The forecast does not contain enough metrics for reliable analysis.")
    {
        ArgumentNullException.ThrowIfNull(missingMetrics);

        MissingMetrics = Array.AsReadOnly(missingMetrics.Distinct().ToArray());
    }

    public IReadOnlyList<ForecastMetricName> MissingMetrics { get; }
}
