using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast.Ports;

/// <summary>
/// Application port for optional tide forecasts.
/// Tide failures may be handled as a degraded result when the analysis does not require tides.
/// </summary>
public interface ITideProvider
{
    string ProviderCode { get; }

    bool IsEnabled { get; }

    Task<ProviderTideResult> GetTidesAsync(
        GeoPoint location,
        ForecastRange range,
        CancellationToken cancellationToken);
}
