using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Providers.OpenMeteo;

public sealed class OpenMeteoWeatherProvider : OpenMeteoForecastProvider, IWeatherForecastProvider
{
    private static readonly string[] HourlyVariables =
    [
        "wind_speed_10m",
        "wind_gusts_10m",
        "wind_direction_10m",
        "temperature_2m",
        "relative_humidity_2m",
        "surface_pressure",
        "cloud_cover",
        "precipitation",
        "cape",
        "visibility",
        "weather_code"
    ];

    public OpenMeteoWeatherProvider(
        HttpClient httpClient,
        IOptions<OpenMeteoOptions> options,
        TimeProvider timeProvider)
        : base(httpClient, options, timeProvider)
    {
    }

    public string ProviderCode => ProviderCodeValue;

    public async Task<ProviderForecastResult> GetWeatherAsync(
        GeoPoint location,
        ForecastRange range,
        CancellationToken cancellationToken)
    {
        var response = await GetResponseAsync(
            location,
            range,
            Options.WeatherBaseUrl,
            Options.WeatherModel,
            HourlyVariables,
            isWeather: true,
            cancellationToken);

        var batch = OpenMeteoForecastNormalizer.NormalizeWeather(
            response,
            location,
            range,
            Options.WeatherModel,
            UtcNow);
        return new ProviderForecastResult(batch);
    }
}
