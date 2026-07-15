namespace MarineInsight.Domain.Forecast;

public sealed class ForecastPoint
{
    public ForecastPoint(
        DateTimeOffset forecastTime,
        ForecastMetricSet metrics,
        DataQuality quality,
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
            throw new ArgumentException("A forecast point cannot contain duplicate metric sources.", nameof(metricSources));
        }

        if (sources.Any(source => source.ForecastTimeUtc != ForecastTimeUtc))
        {
            throw new ArgumentException("Metric source times must match the forecast point time.", nameof(metricSources));
        }

        var sourceMetrics = sources.Select(source => source.Metric).ToHashSet();
        var missingSources = metrics.GetPresentMetrics()
            .Where(metric => !sourceMetrics.Contains(metric))
            .ToArray();

        if (missingSources.Length > 0)
        {
            throw new ArgumentException(
                $"Every populated metric must have a source: {string.Join(", ", missingSources)}.",
                nameof(metricSources));
        }

        MetricSources = Array.AsReadOnly(sources);
    }

    public DateTimeOffset ForecastTimeUtc { get; }

    public ForecastMetricSet Metrics { get; }

    public DataQuality Quality { get; }

    public IReadOnlyList<MetricSource> MetricSources { get; }
}
