using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast.Ports;

/// <summary>
/// Application port for conventional weather forecasts.
/// Implementations must return normalized values and never expose vendor DTOs.
/// </summary>
public interface IWeatherForecastProvider
{
    string ProviderCode { get; }

    Task<ProviderForecastResult> GetWeatherAsync(
        GeoPoint location,
        ForecastRange range,
        CancellationToken cancellationToken);
}
