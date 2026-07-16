using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Providers.OpenMeteo;

public sealed class OpenMeteoMarineProvider : OpenMeteoForecastProvider, IMarineForecastProvider
{
    private static readonly string[] HourlyVariables =
    [
        "wave_height",
        "wave_period",
        "wave_peak_period",
        "wave_direction",
        "wind_wave_height",
        "wind_wave_period",
        "wind_wave_peak_period",
        "wind_wave_direction",
        "swell_wave_height",
        "swell_wave_period",
        "swell_wave_peak_period",
        "swell_wave_direction"
    ];

    public OpenMeteoMarineProvider(
        HttpClient httpClient,
        IOptions<OpenMeteoOptions> options,
        TimeProvider timeProvider)
        : base(httpClient, options, timeProvider)
    {
    }

    public string ProviderCode => ProviderCodeValue;

    public async Task<ProviderForecastResult> GetMarineAsync(
        GeoPoint location,
        ForecastRange range,
        CancellationToken cancellationToken)
    {
        var response = await GetResponseAsync(
            location,
            range,
            Options.MarineBaseUrl,
            Options.MarineModel,
            HourlyVariables,
            isWeather: false,
            cancellationToken);

        var batch = OpenMeteoForecastNormalizer.NormalizeMarine(
            response,
            location,
            range,
            Options.MarineModel,
            UtcNow);
        return new ProviderForecastResult(batch);
    }
}
