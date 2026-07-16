namespace MarineInsight.Domain.Forecast;

public sealed class ForecastBatch
{
    public ForecastBatch(
        Guid batchId,
        ForecastDataDomain dataDomain,
        ProviderIdentity provider,
        GeoPoint requestedLocation,
        GeoPoint? gridLocation,
        DateTimeOffset issuedAt,
        DateTimeOffset fetchedAt,
        ForecastRange range,
        IEnumerable<ForecastPoint> points,
        DataQuality quality)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(quality);

        if (batchId == Guid.Empty)
        {
            throw new ArgumentException("Batch ID is required.", nameof(batchId));
        }

        var orderedPoints = points.ToArray();
        if (orderedPoints.Length == 0)
        {
            throw new ArgumentException("A forecast batch must contain at least one point.", nameof(points));
        }

        for (var index = 0; index < orderedPoints.Length; index++)
        {
            var point = orderedPoints[index];
            if (!range.Contains(point.ForecastTimeUtc))
            {
                throw new ArgumentException("Every forecast point must be inside the requested range.", nameof(points));
            }

            if (index > 0 && orderedPoints[index - 1].ForecastTimeUtc >= point.ForecastTimeUtc)
            {
                throw new ArgumentException("Forecast points must be unique and strictly ascending.", nameof(points));
            }

            if (point.MetricSources.Any(source =>
                    source.BatchId != batchId || source.Provider != provider))
            {
                throw new ArgumentException(
                    "Metric sources must reference the containing batch and provider.",
                    nameof(points));
            }
        }

        BatchId = batchId;
        DataDomain = dataDomain;
        Provider = provider;
        RequestedLocation = requestedLocation;
        GridLocation = gridLocation;
        IssuedAtUtc = issuedAt.ToUniversalTime();
        FetchedAtUtc = fetchedAt.ToUniversalTime();
        Range = range;
        Points = Array.AsReadOnly(orderedPoints);
        Quality = quality;
    }

    public Guid BatchId { get; }

    public ForecastDataDomain DataDomain { get; }

    public ProviderIdentity Provider { get; }

    public GeoPoint RequestedLocation { get; }

    public GeoPoint? GridLocation { get; }

    public DateTimeOffset IssuedAtUtc { get; }

    public DateTimeOffset FetchedAtUtc { get; }

    public ForecastRange Range { get; }

    public IReadOnlyList<ForecastPoint> Points { get; }

    public DataQuality Quality { get; }

    /// <summary>
    /// Marks every level of an old provider batch as stale without changing its original
    /// fetched timestamp, values, missing metrics, or source references.
    /// </summary>
    public ForecastBatch AsStale() => new(
        BatchId,
        DataDomain,
        Provider,
        RequestedLocation,
        GridLocation,
        IssuedAtUtc,
        FetchedAtUtc,
        Range,
        Points.Select(point => point.AsStale()),
        Quality.AsStale());
}
