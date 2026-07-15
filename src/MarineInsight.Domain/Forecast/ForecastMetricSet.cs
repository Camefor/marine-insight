namespace MarineInsight.Domain.Forecast;

public sealed record ForecastMetricSet
{
    private ForecastMetricSet()
    {
    }

    public double? WindSpeedMs { get; private init; }

    public double? WindGustMs { get; private init; }

    public double? WindDirectionDeg { get; private init; }

    public double? TemperatureC { get; private init; }

    public double? RelativeHumidityPct { get; private init; }

    public double? SurfacePressureHpa { get; private init; }

    public double? CloudCoverPct { get; private init; }

    public double? PrecipitationMmPerHour { get; private init; }

    public double? CapeJkg { get; private init; }

    public double? VisibilityM { get; private init; }

    public int? WeatherCode { get; private init; }

    public bool? Thunderstorm { get; private init; }

    public double? WaveHeightM { get; private init; }

    public double? WavePeriodS { get; private init; }

    public double? WavePeakPeriodS { get; private init; }

    public double? WaveDirectionDeg { get; private init; }

    public double? WindWaveHeightM { get; private init; }

    public double? WindWavePeriodS { get; private init; }

    public double? WindWavePeakPeriodS { get; private init; }

    public double? WindWaveDirectionDeg { get; private init; }

    public double? SwellHeightM { get; private init; }

    public double? SwellPeriodS { get; private init; }

    public double? SwellPeakPeriodS { get; private init; }

    public double? SwellDirectionDeg { get; private init; }

    public double? SeaTemperatureC { get; private init; }

    public double? CurrentSpeedMs { get; private init; }

    public double? CurrentDirectionDeg { get; private init; }

    public double? TideHeightM { get; private init; }

    public TideType? TideType { get; private init; }

    public static ForecastMetricSet Create(
        double? windSpeedMs = null,
        double? windGustMs = null,
        double? windDirectionDeg = null,
        double? temperatureC = null,
        double? relativeHumidityPct = null,
        double? surfacePressureHpa = null,
        double? cloudCoverPct = null,
        double? precipitationMmPerHour = null,
        double? capeJkg = null,
        double? visibilityM = null,
        int? weatherCode = null,
        bool? thunderstorm = null,
        double? waveHeightM = null,
        double? wavePeriodS = null,
        double? wavePeakPeriodS = null,
        double? waveDirectionDeg = null,
        double? windWaveHeightM = null,
        double? windWavePeriodS = null,
        double? windWavePeakPeriodS = null,
        double? windWaveDirectionDeg = null,
        double? swellHeightM = null,
        double? swellPeriodS = null,
        double? swellPeakPeriodS = null,
        double? swellDirectionDeg = null,
        double? seaTemperatureC = null,
        double? currentSpeedMs = null,
        double? currentDirectionDeg = null,
        double? tideHeightM = null,
        TideType? tideType = null)
    {
        var metrics = new ForecastMetricSet
        {
            WindSpeedMs = windSpeedMs,
            WindGustMs = windGustMs,
            WindDirectionDeg = windDirectionDeg,
            TemperatureC = temperatureC,
            RelativeHumidityPct = relativeHumidityPct,
            SurfacePressureHpa = surfacePressureHpa,
            CloudCoverPct = cloudCoverPct,
            PrecipitationMmPerHour = precipitationMmPerHour,
            CapeJkg = capeJkg,
            VisibilityM = visibilityM,
            WeatherCode = weatherCode,
            Thunderstorm = thunderstorm,
            WaveHeightM = waveHeightM,
            WavePeriodS = wavePeriodS,
            WavePeakPeriodS = wavePeakPeriodS,
            WaveDirectionDeg = waveDirectionDeg,
            WindWaveHeightM = windWaveHeightM,
            WindWavePeriodS = windWavePeriodS,
            WindWavePeakPeriodS = windWavePeakPeriodS,
            WindWaveDirectionDeg = windWaveDirectionDeg,
            SwellHeightM = swellHeightM,
            SwellPeriodS = swellPeriodS,
            SwellPeakPeriodS = swellPeakPeriodS,
            SwellDirectionDeg = swellDirectionDeg,
            SeaTemperatureC = seaTemperatureC,
            CurrentSpeedMs = currentSpeedMs,
            CurrentDirectionDeg = currentDirectionDeg,
            TideHeightM = tideHeightM,
            TideType = tideType
        };

        metrics.Validate();
        return metrics;
    }

    public IReadOnlyCollection<ForecastMetricName> GetPresentMetrics()
    {
        var metrics = new List<ForecastMetricName>();

        AddIfPresent(metrics, ForecastMetricName.WindSpeedMs, WindSpeedMs.HasValue);
        AddIfPresent(metrics, ForecastMetricName.WindGustMs, WindGustMs.HasValue);
        AddIfPresent(metrics, ForecastMetricName.WindDirectionDeg, WindDirectionDeg.HasValue);
        AddIfPresent(metrics, ForecastMetricName.TemperatureC, TemperatureC.HasValue);
        AddIfPresent(metrics, ForecastMetricName.RelativeHumidityPct, RelativeHumidityPct.HasValue);
        AddIfPresent(metrics, ForecastMetricName.SurfacePressureHpa, SurfacePressureHpa.HasValue);
        AddIfPresent(metrics, ForecastMetricName.CloudCoverPct, CloudCoverPct.HasValue);
        AddIfPresent(metrics, ForecastMetricName.PrecipitationMmPerHour, PrecipitationMmPerHour.HasValue);
        AddIfPresent(metrics, ForecastMetricName.CapeJkg, CapeJkg.HasValue);
        AddIfPresent(metrics, ForecastMetricName.VisibilityM, VisibilityM.HasValue);
        AddIfPresent(metrics, ForecastMetricName.WeatherCode, WeatherCode.HasValue);
        AddIfPresent(metrics, ForecastMetricName.Thunderstorm, Thunderstorm.HasValue);
        AddIfPresent(metrics, ForecastMetricName.WaveHeightM, WaveHeightM.HasValue);
        AddIfPresent(metrics, ForecastMetricName.WavePeriodS, WavePeriodS.HasValue);
        AddIfPresent(metrics, ForecastMetricName.WavePeakPeriodS, WavePeakPeriodS.HasValue);
        AddIfPresent(metrics, ForecastMetricName.WaveDirectionDeg, WaveDirectionDeg.HasValue);
        AddIfPresent(metrics, ForecastMetricName.WindWaveHeightM, WindWaveHeightM.HasValue);
        AddIfPresent(metrics, ForecastMetricName.WindWavePeriodS, WindWavePeriodS.HasValue);
        AddIfPresent(metrics, ForecastMetricName.WindWavePeakPeriodS, WindWavePeakPeriodS.HasValue);
        AddIfPresent(metrics, ForecastMetricName.WindWaveDirectionDeg, WindWaveDirectionDeg.HasValue);
        AddIfPresent(metrics, ForecastMetricName.SwellHeightM, SwellHeightM.HasValue);
        AddIfPresent(metrics, ForecastMetricName.SwellPeriodS, SwellPeriodS.HasValue);
        AddIfPresent(metrics, ForecastMetricName.SwellPeakPeriodS, SwellPeakPeriodS.HasValue);
        AddIfPresent(metrics, ForecastMetricName.SwellDirectionDeg, SwellDirectionDeg.HasValue);
        AddIfPresent(metrics, ForecastMetricName.SeaTemperatureC, SeaTemperatureC.HasValue);
        AddIfPresent(metrics, ForecastMetricName.CurrentSpeedMs, CurrentSpeedMs.HasValue);
        AddIfPresent(metrics, ForecastMetricName.CurrentDirectionDeg, CurrentDirectionDeg.HasValue);
        AddIfPresent(metrics, ForecastMetricName.TideHeightM, TideHeightM.HasValue);
        AddIfPresent(metrics, ForecastMetricName.TideType, TideType.HasValue);

        return metrics.AsReadOnly();
    }

    private void Validate()
    {
        EnsureFinite(nameof(WindSpeedMs), WindSpeedMs);
        EnsureFinite(nameof(WindGustMs), WindGustMs);
        EnsureFinite(nameof(WindDirectionDeg), WindDirectionDeg);
        EnsureFinite(nameof(TemperatureC), TemperatureC);
        EnsureFinite(nameof(RelativeHumidityPct), RelativeHumidityPct);
        EnsureFinite(nameof(SurfacePressureHpa), SurfacePressureHpa);
        EnsureFinite(nameof(CloudCoverPct), CloudCoverPct);
        EnsureFinite(nameof(PrecipitationMmPerHour), PrecipitationMmPerHour);
        EnsureFinite(nameof(CapeJkg), CapeJkg);
        EnsureFinite(nameof(VisibilityM), VisibilityM);
        EnsureFinite(nameof(WaveHeightM), WaveHeightM);
        EnsureFinite(nameof(WavePeriodS), WavePeriodS);
        EnsureFinite(nameof(WavePeakPeriodS), WavePeakPeriodS);
        EnsureFinite(nameof(WaveDirectionDeg), WaveDirectionDeg);
        EnsureFinite(nameof(WindWaveHeightM), WindWaveHeightM);
        EnsureFinite(nameof(WindWavePeriodS), WindWavePeriodS);
        EnsureFinite(nameof(WindWavePeakPeriodS), WindWavePeakPeriodS);
        EnsureFinite(nameof(WindWaveDirectionDeg), WindWaveDirectionDeg);
        EnsureFinite(nameof(SwellHeightM), SwellHeightM);
        EnsureFinite(nameof(SwellPeriodS), SwellPeriodS);
        EnsureFinite(nameof(SwellPeakPeriodS), SwellPeakPeriodS);
        EnsureFinite(nameof(SwellDirectionDeg), SwellDirectionDeg);
        EnsureFinite(nameof(SeaTemperatureC), SeaTemperatureC);
        EnsureFinite(nameof(CurrentSpeedMs), CurrentSpeedMs);
        EnsureFinite(nameof(CurrentDirectionDeg), CurrentDirectionDeg);
        EnsureFinite(nameof(TideHeightM), TideHeightM);
    }

    private static void AddIfPresent(
        List<ForecastMetricName> metrics,
        ForecastMetricName metric,
        bool isPresent)
    {
        if (isPresent)
        {
            metrics.Add(metric);
        }
    }

    private static void EnsureFinite(string parameterName, double? value)
    {
        if (value.HasValue && !double.IsFinite(value.Value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Forecast metric values must be finite or null.");
        }
    }
}
