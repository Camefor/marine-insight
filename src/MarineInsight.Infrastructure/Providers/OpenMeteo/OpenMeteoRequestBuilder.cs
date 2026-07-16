using System.Globalization;
using MarineInsight.Domain.Forecast;
using Microsoft.AspNetCore.WebUtilities;

namespace MarineInsight.Infrastructure.Providers.OpenMeteo;

internal static class OpenMeteoRequestBuilder
{
    public static Uri Build(
        string endpoint,
        GeoPoint location,
        ForecastRange range,
        string model,
        IReadOnlyCollection<string> hourlyVariables,
        bool isWeather,
        string? apiKey)
    {
        var query = new Dictionary<string, string?>
        {
            ["latitude"] = location.Latitude.ToString("F6", CultureInfo.InvariantCulture),
            ["longitude"] = location.Longitude.ToString("F6", CultureInfo.InvariantCulture),
            ["hourly"] = string.Join(',', hourlyVariables),
            ["models"] = model,
            ["start_date"] = range.StartUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["end_date"] = range.EndUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["timezone"] = "UTC",
            ["timeformat"] = "iso8601"
        };

        if (isWeather)
        {
            query["wind_speed_unit"] = "ms";
            query["temperature_unit"] = "celsius";
            query["precipitation_unit"] = "mm";
        }

        // Open-Meteo commercial endpoints accept the key as a query parameter. The URI is
        // deliberately kept inside the HTTP adapter and is never included in log messages.
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            query["apikey"] = apiKey.Trim();
        }

        var uriText = QueryHelpers.AddQueryString(endpoint, query);
        return new Uri(uriText, UriKind.Absolute);
    }
}
