using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Caching;

/// <summary>
/// Keeps environment, coordinate precision, and normalizer version consistent for all callers.
/// </summary>
public sealed class ForecastCacheKeyFactory : IForecastCacheKeyFactory
{
    private readonly IOptions<ForecastCacheOptions> _options;

    public ForecastCacheKeyFactory(IOptions<ForecastCacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public ForecastCacheKey Create(
        ForecastDataDomain dataDomain,
        ProviderIdentity provider,
        GeoPoint location,
        ForecastRange range)
    {
        var value = _options.Value;
        value.Validate();

        return ForecastCacheKey.Create(
            value.Environment,
            dataDomain,
            provider,
            location,
            range,
            value.NormalizerVersion,
            value.CoordinatePrecision);
    }

    public ForecastCachePolicy Policy => _options.Value.ToPolicy();
}
