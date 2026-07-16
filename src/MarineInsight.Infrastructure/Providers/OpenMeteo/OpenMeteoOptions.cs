using System.ComponentModel.DataAnnotations;

namespace MarineInsight.Infrastructure.Providers.OpenMeteo;

public sealed class OpenMeteoOptions
{
    public const string SectionName = "ForecastProviders:OpenMeteo";

    public bool Enabled { get; set; } = true;

    [Required, Url]
    public string WeatherBaseUrl { get; set; } = "https://api.open-meteo.com/v1/forecast";

    [Required, Url]
    public string MarineBaseUrl { get; set; } = "https://marine-api.open-meteo.com/v1/marine";

    public string WeatherModel { get; set; } = "best_match";

    public string MarineModel { get; set; } = "best_match";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    public string? ApiKey { get; set; }

    public void Validate()
    {
        ValidateEndpoint(WeatherBaseUrl, nameof(WeatherBaseUrl));
        ValidateEndpoint(MarineBaseUrl, nameof(MarineBaseUrl));

        if (string.IsNullOrWhiteSpace(WeatherModel))
        {
            throw new InvalidOperationException("Open-Meteo weather model is required.");
        }

        if (string.IsNullOrWhiteSpace(MarineModel))
        {
            throw new InvalidOperationException("Open-Meteo marine model is required.");
        }

        if (Timeout < TimeSpan.FromSeconds(1) || Timeout > TimeSpan.FromMinutes(2))
        {
            throw new InvalidOperationException("Open-Meteo timeout must be between 1 and 120 seconds.");
        }
    }

    private static void ValidateEndpoint(string endpoint, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(endpoint) ||
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Open-Meteo {parameterName} must be an absolute HTTP(S) URL.");
        }
    }
}
