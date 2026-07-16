namespace MarineInsight.Domain.Forecast;

public sealed class ForecastSnapshotPoint
{
    public ForecastSnapshotPoint(
        DateTimeOffset forecastTime,
        ForecastMetricSet metrics,
        SnapshotQuality quality,
        IEnumerable<MetricSource> metricSources)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(quality);
        ArgumentNullException.ThrowIfNull(metricSources);

        ForecastTimeUtc = forecastTime.ToUniversalTime();
        Metrics = metrics;
        Quality = quality;

        var sources = metricSources.ToArray();
        if (sources.Select(source => source.Metric).Distinct().Count() != sources.Length)
        {
            throw new ArgumentException("A snapshot point cannot contain duplicate metric sources.", nameof(metricSources));
        }

        var presentMetrics = metrics.GetPresentMetrics().ToHashSet();
        if (sources.Any(source => !presentMetrics.Contains(source.Metric)))
        {
            throw new ArgumentException(
                "Snapshot metric sources must reference populated metrics.",
                nameof(metricSources));
        }

        var missingSources = presentMetrics
            .Where(metric => sources.All(source => source.Metric != metric))
            .ToArray();
        if (missingSources.Length > 0)
        {
            throw new ArgumentException(
                $"Every populated snapshot metric must have a source: {string.Join(", ", missingSources)}.",
                nameof(metricSources));
        }

        MetricSources = Array.AsReadOnly(sources);
    }

    public DateTimeOffset ForecastTimeUtc { get; }

    public ForecastMetricSet Metrics { get; }

    public SnapshotQuality Quality { get; }

    public IReadOnlyList<MetricSource> MetricSources { get; }
}
