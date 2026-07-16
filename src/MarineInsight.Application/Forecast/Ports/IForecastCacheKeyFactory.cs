using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast.Ports;

/// <summary>
/// Provides one consistent cache-key namespace and policy to Application use cases.
/// </summary>
public interface IForecastCacheKeyFactory
{
    ForecastCacheKey Create(
        ForecastDataDomain dataDomain,
        ProviderIdentity provider,
        GeoPoint location,
        ForecastRange range);

    ForecastCachePolicy Policy { get; }
}
