using MarineInsight.Domain.Forecast;

namespace MarineInsight.Domain.Analysis;

/// <summary>
/// Reference from a persisted analysis result to one of the forecast batches that
/// produced it. Captures the batch identity, the data domain it contributed, and the
/// role it played so a historical result stays traceable to its inputs.
/// </summary>
public sealed record AnalysisSourceBatch
{
    public AnalysisSourceBatch(
        Guid batchId,
        ForecastDataDomain dataDomain,
        string providerCode,
        string sourceModel,
        AnalysisSourceRole sourceRole,
        string selectionPolicy)
    {
        if (batchId == Guid.Empty)
        {
            throw new ArgumentException("Batch ID is required.", nameof(batchId));
        }

        if (string.IsNullOrWhiteSpace(providerCode))
        {
            throw new ArgumentException("Provider code is required.", nameof(providerCode));
        }

        if (string.IsNullOrWhiteSpace(sourceModel))
        {
            throw new ArgumentException("Source model is required.", nameof(sourceModel));
        }

        if (string.IsNullOrWhiteSpace(selectionPolicy))
        {
            throw new ArgumentException("Selection policy is required.", nameof(selectionPolicy));
        }

        BatchId = batchId;
        DataDomain = dataDomain;
        ProviderCode = providerCode;
        SourceModel = sourceModel;
        SourceRole = sourceRole;
        SelectionPolicy = selectionPolicy;
    }

    public Guid BatchId { get; }

    public ForecastDataDomain DataDomain { get; }

    public string ProviderCode { get; }

    public string SourceModel { get; }

    public AnalysisSourceRole SourceRole { get; }

    public string SelectionPolicy { get; }
}
