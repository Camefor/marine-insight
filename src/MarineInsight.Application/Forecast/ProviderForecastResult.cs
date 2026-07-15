using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast;

/// <summary>
/// Standardized result returned by a Weather or Marine provider after its anti-corruption mapping.
/// </summary>
public sealed record ProviderForecastResult
{
    public ProviderForecastResult(ForecastBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.DataDomain is not (ForecastDataDomain.Weather or ForecastDataDomain.Marine))
        {
            throw new ArgumentException("Weather and Marine results require a matching forecast data domain.", nameof(batch));
        }

        Batch = batch;
    }

    public ForecastBatch Batch { get; }
}
