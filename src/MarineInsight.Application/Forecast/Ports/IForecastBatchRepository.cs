using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast.Ports;

/// <summary>
/// Application boundary for append-only normalized forecast batches.
/// </summary>
public interface IForecastBatchRepository
{
    /// <summary>
    /// Appends one provider batch for the selected location without updating existing rows.
    /// </summary>
    Task AppendAsync(
        Guid locationId,
        ForecastBatch batch,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one complete batch, including points and metric-level source references.
    /// </summary>
    Task<ForecastBatch?> GetByIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads batches covering the requested 24, 72, or 168 hour UTC range.
    /// </summary>
    Task<IReadOnlyList<ForecastBatch>> FindAsync(
        Guid locationId,
        ProviderIdentity provider,
        ForecastDataDomain dataDomain,
        ForecastRange range,
        CancellationToken cancellationToken = default);
}
