using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast.Ports;

/// <summary>
/// Application port for waves, wind waves, and swell forecasts.
/// Implementations must preserve the source model and direction semantics in the batch.
/// </summary>
public interface IMarineForecastProvider
{
    string ProviderCode { get; }

    /// <summary>
    /// Identifies the configured provider/model request used for cache partitioning.
    /// The response may report a more concrete model selected by the upstream service.
    /// </summary>
    ProviderIdentity Identity { get; }

    Task<ProviderForecastResult> GetMarineAsync(
        GeoPoint location,
        ForecastRange range,
        CancellationToken cancellationToken);
}
