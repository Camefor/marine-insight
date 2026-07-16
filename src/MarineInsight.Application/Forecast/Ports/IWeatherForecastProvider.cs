using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast.Ports;

/// <summary>
/// Application port for conventional weather forecasts.
/// Implementations must return normalized values and never expose vendor DTOs.
/// </summary>
public interface IWeatherForecastProvider
{
    string ProviderCode { get; }

    /// <summary>
    /// Identifies the configured provider/model request used for cache partitioning.
    /// The response may report a more concrete model selected by the upstream service.
    /// </summary>
    ProviderIdentity Identity { get; }

    Task<ProviderForecastResult> GetWeatherAsync(
        GeoPoint location,
        ForecastRange range,
        CancellationToken cancellationToken);
}
