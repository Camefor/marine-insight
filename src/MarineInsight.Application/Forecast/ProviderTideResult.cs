using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast;

/// <summary>
/// Standardized result returned by a Tide provider after its anti-corruption mapping.
/// </summary>
public sealed record ProviderTideResult
{
    public ProviderTideResult(
        ForecastBatch batch,
        bool fromCache = false,
        int? remainingCredits = null,
        bool creditWarning = false)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.DataDomain != ForecastDataDomain.Tide)
        {
            throw new ArgumentException("Tide results require the Tide forecast data domain.", nameof(batch));
        }

        Batch = batch;
        FromCache = fromCache;
        RemainingCredits = remainingCredits;
        CreditWarning = creditWarning;
    }

    public ForecastBatch Batch { get; }

    public bool FromCache { get; }

    public int? RemainingCredits { get; }

    public bool CreditWarning { get; }
}
