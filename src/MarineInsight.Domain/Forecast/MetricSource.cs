namespace MarineInsight.Domain.Forecast;

public sealed record MetricSource
{
    public MetricSource(
        ForecastMetricName metric,
        ProviderIdentity provider,
        Guid batchId,
        DateTimeOffset forecastTime,
        ForecastQualityStatus qualityStatus,
        ForecastFreshness freshness,
        ForecastQualityMask qualityFlags = ForecastQualityMask.None)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (batchId == Guid.Empty)
        {
            throw new ArgumentException("Batch ID is required.", nameof(batchId));
        }

        Metric = metric;
        Provider = provider;
        BatchId = batchId;
        ForecastTimeUtc = forecastTime.ToUniversalTime();
        QualityStatus = qualityStatus;
        Freshness = freshness;
        QualityFlags = qualityFlags;
    }

    public ForecastMetricName Metric { get; }

    public ProviderIdentity Provider { get; }

    public Guid BatchId { get; }

    public DateTimeOffset ForecastTimeUtc { get; }

    public ForecastQualityStatus QualityStatus { get; }

    public ForecastFreshness Freshness { get; }

    public ForecastQualityMask QualityFlags { get; }

    public MetricSource AsStale()
    {
        var status = QualityStatus is ForecastQualityStatus.Invalid or ForecastQualityStatus.Unknown
            ? QualityStatus
            : ForecastQualityStatus.Stale;

        return new MetricSource(
            Metric,
            Provider,
            BatchId,
            ForecastTimeUtc,
            status,
            ForecastFreshness.Stale,
            QualityFlags | ForecastQualityMask.StaleData);
    }
}
