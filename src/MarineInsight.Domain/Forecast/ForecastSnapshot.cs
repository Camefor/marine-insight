namespace MarineInsight.Domain.Forecast;

public sealed class ForecastSnapshot
{
    public ForecastSnapshot(
        Guid snapshotId,
        GeoPoint requestedLocation,
        ForecastRange range,
        IEnumerable<ForecastSnapshotPoint> points,
        IEnumerable<SourceBatchReference> sourceBatches,
        SnapshotQuality quality)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(sourceBatches);
        ArgumentNullException.ThrowIfNull(quality);

        if (snapshotId == Guid.Empty)
        {
            throw new ArgumentException("Snapshot ID is required.", nameof(snapshotId));
        }

        var orderedPoints = points.ToArray();
        if (orderedPoints.Length == 0)
        {
            throw new ArgumentException("A forecast snapshot must contain at least one point.", nameof(points));
        }

        var references = sourceBatches.ToArray();
        if (references.Length == 0)
        {
            throw new ArgumentException("A forecast snapshot must reference at least one source batch.", nameof(sourceBatches));
        }

        if (references.Select(reference => reference.BatchId).Distinct().Count() != references.Length)
        {
            throw new ArgumentException("A forecast snapshot cannot contain duplicate source batches.", nameof(sourceBatches));
        }

        if (references.Any(reference => reference.RequestedLocation != requestedLocation))
        {
            throw new ArgumentException(
                "Every source batch must use the snapshot requested location.",
                nameof(sourceBatches));
        }

        var referencesById = references.ToDictionary(reference => reference.BatchId);
        for (var index = 0; index < orderedPoints.Length; index++)
        {
            var point = orderedPoints[index];
            if (!range.Contains(point.ForecastTimeUtc))
            {
                throw new ArgumentException("Every snapshot point must be inside the requested range.", nameof(points));
            }

            if (index > 0 && orderedPoints[index - 1].ForecastTimeUtc >= point.ForecastTimeUtc)
            {
                throw new ArgumentException("Snapshot points must be unique and strictly ascending.", nameof(points));
            }

            foreach (var source in point.MetricSources)
            {
                if (!referencesById.TryGetValue(source.BatchId, out var reference) ||
                    reference.Provider != source.Provider)
                {
                    throw new ArgumentException(
                        "Snapshot metric sources must reference a listed source batch and provider.",
                        nameof(points));
                }
            }
        }

        SnapshotId = snapshotId;
        RequestedLocation = requestedLocation;
        Range = range;
        Points = Array.AsReadOnly(orderedPoints);
        SourceBatches = Array.AsReadOnly(references);
        Quality = quality;
    }

    public Guid SnapshotId { get; }

    public GeoPoint RequestedLocation { get; }

    public ForecastRange Range { get; }

    public IReadOnlyList<ForecastSnapshotPoint> Points { get; }

    public IReadOnlyList<SourceBatchReference> SourceBatches { get; }

    public SnapshotQuality Quality { get; }
}
