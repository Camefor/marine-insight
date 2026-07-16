using System.Text.Json.Serialization;

namespace MarineInsight.Infrastructure.Providers.OpenMeteo;

public sealed class OpenMeteoForecastResponse
{
    [JsonPropertyName("latitude")]
    public double? Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("hourly")]
    public OpenMeteoHourlyData? Hourly { get; init; }
}

public sealed class OpenMeteoHourlyData
{
    [JsonPropertyName("time")]
    public string?[]? Time { get; init; }

    [JsonPropertyName("wind_speed_10m")]
    public double?[]? WindSpeedMs { get; init; }

    [JsonPropertyName("wind_gusts_10m")]
    public double?[]? WindGustMs { get; init; }

    [JsonPropertyName("wind_direction_10m")]
    public double?[]? WindDirectionDeg { get; init; }

    [JsonPropertyName("temperature_2m")]
    public double?[]? TemperatureC { get; init; }

    [JsonPropertyName("relative_humidity_2m")]
    public double?[]? RelativeHumidityPct { get; init; }

    [JsonPropertyName("surface_pressure")]
    public double?[]? SurfacePressureHpa { get; init; }

    [JsonPropertyName("cloud_cover")]
    public double?[]? CloudCoverPct { get; init; }

    [JsonPropertyName("precipitation")]
    public double?[]? PrecipitationMm { get; init; }

    [JsonPropertyName("cape")]
    public double?[]? CapeJkg { get; init; }

    [JsonPropertyName("visibility")]
    public double?[]? VisibilityM { get; init; }

    [JsonPropertyName("weather_code")]
    public int?[]? WeatherCode { get; init; }

    [JsonPropertyName("wave_height")]
    public double?[]? WaveHeightM { get; init; }

    [JsonPropertyName("wave_period")]
    public double?[]? WavePeriodS { get; init; }

    [JsonPropertyName("wave_peak_period")]
    public double?[]? WavePeakPeriodS { get; init; }

    [JsonPropertyName("wave_direction")]
    public double?[]? WaveDirectionDeg { get; init; }

    [JsonPropertyName("wind_wave_height")]
    public double?[]? WindWaveHeightM { get; init; }

    [JsonPropertyName("wind_wave_period")]
    public double?[]? WindWavePeriodS { get; init; }

    [JsonPropertyName("wind_wave_peak_period")]
    public double?[]? WindWavePeakPeriodS { get; init; }

    [JsonPropertyName("wind_wave_direction")]
    public double?[]? WindWaveDirectionDeg { get; init; }

    [JsonPropertyName("swell_wave_height")]
    public double?[]? SwellHeightM { get; init; }

    [JsonPropertyName("swell_wave_period")]
    public double?[]? SwellPeriodS { get; init; }

    [JsonPropertyName("swell_wave_peak_period")]
    public double?[]? SwellPeakPeriodS { get; init; }

    [JsonPropertyName("swell_wave_direction")]
    public double?[]? SwellDirectionDeg { get; init; }
}
