namespace MarineInsight.Domain.Forecast;

public sealed record SourceBatchReference
{
    public SourceBatchReference(
        Guid batchId,
        ForecastDataDomain dataDomain,
        ProviderIdentity provider,
        GeoPoint requestedLocation,
        GeoPoint? gridLocation,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset fetchedAtUtc,
        ForecastRange range,
        DataQuality quality)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(quality);

        if (batchId == Guid.Empty)
        {
            throw new ArgumentException("Batch ID is required.", nameof(batchId));
        }

        BatchId = batchId;
        DataDomain = dataDomain;
        Provider = provider;
        RequestedLocation = requestedLocation;
        GridLocation = gridLocation;
        IssuedAtUtc = issuedAtUtc.ToUniversalTime();
        FetchedAtUtc = fetchedAtUtc.ToUniversalTime();
        Range = range;
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

    public DataQuality Quality { get; }

    public static SourceBatchReference FromBatch(ForecastBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return new SourceBatchReference(
            batch.BatchId,
            batch.DataDomain,
            batch.Provider,
            batch.RequestedLocation,
            batch.GridLocation,
            batch.IssuedAtUtc,
            batch.FetchedAtUtc,
            batch.Range,
            batch.Quality);
    }
}
