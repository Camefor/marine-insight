using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast;

/// <summary>
/// Standardized result returned by a Tide provider after its anti-corruption mapping.
/// </summary>
public sealed record ProviderTideResult
{
    public ProviderTideResult(ForecastBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.DataDomain != ForecastDataDomain.Tide)
        {
            throw new ArgumentException("Tide results require the Tide forecast data domain.", nameof(batch));
        }

        Batch = batch;
    }

    public ForecastBatch Batch { get; }
}
