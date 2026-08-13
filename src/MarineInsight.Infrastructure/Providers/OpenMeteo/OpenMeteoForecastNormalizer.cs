using System.Globalization;
using MarineInsight.Application.Errors;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Infrastructure.Providers.OpenMeteo;

internal static class OpenMeteoForecastNormalizer
{
    private static readonly ForecastMetricName[] WeatherMetrics =
    [
        ForecastMetricName.WindSpeedMs,
        ForecastMetricName.WindGustMs,
        ForecastMetricName.WindDirectionDeg,
        ForecastMetricName.TemperatureC,
        ForecastMetricName.RelativeHumidityPct,
        ForecastMetricName.SurfacePressureHpa,
        ForecastMetricName.CloudCoverPct,
        ForecastMetricName.PrecipitationMmPerHour,
        ForecastMetricName.CapeJkg,
        ForecastMetricName.VisibilityM,
        ForecastMetricName.WeatherCode,
        ForecastMetricName.Thunderstorm
    ];

    private static readonly ForecastMetricName[] MarineMetrics =
    [
        ForecastMetricName.WaveHeightM,
        ForecastMetricName.WavePeriodS,
        ForecastMetricName.WaveDirectionDeg,
        ForecastMetricName.WindWaveHeightM,
        ForecastMetricName.WindWavePeriodS,
        ForecastMetricName.WindWaveDirectionDeg,
        ForecastMetricName.SwellHeightM,
        ForecastMetricName.SwellPeriodS,
        ForecastMetricName.SwellDirectionDeg
    ];

    public static ForecastBatch NormalizeWeather(
        OpenMeteoForecastResponse response,
        GeoPoint requestedLocation,
        ForecastRange range,
        string configuredModel,
        DateTimeOffset fetchedAtUtc)
    {
        return Normalize(
            response,
            requestedLocation,
            range,
            ForecastDataDomain.Weather,
            configuredModel,
            fetchedAtUtc,
            WeatherMetrics,
            MapWeatherPoint);
    }

    public static ForecastBatch NormalizeMarine(
        OpenMeteoForecastResponse response,
        GeoPoint requestedLocation,
        ForecastRange range,
        string configuredModel,
        DateTimeOffset fetchedAtUtc)
    {
        return Normalize(
            response,
            requestedLocation,
            range,
            ForecastDataDomain.Marine,
            configuredModel,
            fetchedAtUtc,
            MarineMetrics,
            MapMarinePoint);
    }

    private static ForecastBatch Normalize(
        OpenMeteoForecastResponse response,
        GeoPoint requestedLocation,
        ForecastRange range,
        ForecastDataDomain dataDomain,
        string configuredModel,
        DateTimeOffset fetchedAtUtc,
        IReadOnlyCollection<ForecastMetricName> expectedMetrics,
        Func<OpenMeteoHourlyData, int, NormalizedPoint> mapPoint)
    {
        ArgumentNullException.ThrowIfNull(response);

        var hourly = response.Hourly ?? throw Contract("The Open-Meteo response does not contain hourly data.");
        var times = ParseTimes(hourly.Time);
        ValidateArrayLengths(hourly, times.Length, dataDomain);

        var gridLocation = ParseGridLocation(response);
        var selected = times
            .Select((time, index) => (time, index))
            .Where(item => range.Contains(item.time))
            .ToArray();

        if (selected.Length == 0)
        {
            throw Contract("The Open-Meteo response does not contain points in the requested UTC range.");
        }

        var batchId = Guid.NewGuid();
        var provider = new ProviderIdentity(
            "open-meteo",
            string.IsNullOrWhiteSpace(response.Model) ? configuredModel : response.Model);
        var points = selected
            .Select(item => CreatePoint(item.time, mapPoint(hourly, item.index), provider, batchId))
            .ToArray();

        return new ForecastBatch(
            batchId,
            dataDomain,
            provider,
            requestedLocation,
            gridLocation,
            // Open-Meteo does not expose a forecast issue timestamp in this payload. The fetch
            // instant is the only honest timestamp until a source-specific issue field is added.
            fetchedAtUtc,
            fetchedAtUtc,
            range,
            points,
            AggregateQuality(points, expectedMetrics));
    }

    private static ForecastPoint CreatePoint(
        DateTimeOffset time,
        NormalizedPoint normalizedPoint,
        ProviderIdentity provider,
        Guid batchId)
    {
        var sources = normalizedPoint.Metrics
            .GetPresentMetrics()
            .Select(metric => new MetricSource(
                metric,
                provider,
                batchId,
                time,
                ForecastQualityStatus.Valid,
                ForecastFreshness.Fresh,
                normalizedPoint.GetSourceFlags(metric)))
            .ToArray();

        return new ForecastPoint(time, normalizedPoint.Metrics, normalizedPoint.Quality, sources);
    }

    private static NormalizedPoint MapWeatherPoint(OpenMeteoHourlyData hourly, int index)
    {
        var metrics = new MetricAccumulator(WeatherMetrics);
        var windSpeed = metrics.ReadDouble(
            ForecastMetricName.WindSpeedMs,
            hourly.WindSpeedMs,
            index,
            IsNonNegative);
        var windGust = metrics.ReadDouble(
            ForecastMetricName.WindGustMs,
            hourly.WindGustMs,
            index,
            IsNonNegative);
        var windDirection = metrics.ReadDouble(
            ForecastMetricName.WindDirectionDeg,
            hourly.WindDirectionDeg,
            index,
            IsDirection,
            NormalizeDirection);
        var temperature = metrics.ReadDouble(
            ForecastMetricName.TemperatureC,
            hourly.TemperatureC,
            index);
        var humidity = metrics.ReadDouble(
            ForecastMetricName.RelativeHumidityPct,
            hourly.RelativeHumidityPct,
            index,
            IsPercentage);
        var pressure = metrics.ReadDouble(
            ForecastMetricName.SurfacePressureHpa,
            hourly.SurfacePressureHpa,
            index,
            value => value > 0);
        var cloudCover = metrics.ReadDouble(
            ForecastMetricName.CloudCoverPct,
            hourly.CloudCoverPct,
            index,
            IsPercentage);
        var precipitation = metrics.ReadDouble(
            ForecastMetricName.PrecipitationMmPerHour,
            hourly.PrecipitationMm,
            index,
            IsNonNegative);
        var cape = metrics.ReadDouble(
            ForecastMetricName.CapeJkg,
            hourly.CapeJkg,
            index,
            IsNonNegative);
        var visibility = metrics.ReadDouble(
            ForecastMetricName.VisibilityM,
            hourly.VisibilityM,
            index,
            IsNonNegative);
        var weatherCode = metrics.ReadInt(
            ForecastMetricName.WeatherCode,
            hourly.WeatherCode,
            index,
            value => value is >= 0 and <= 99);
        var thunderstormFlags = metrics.GetFlags(ForecastMetricName.WeatherCode);
        var thunderstorm = weatherCode.HasValue
            ? metrics.MarkPresent(
                ForecastMetricName.Thunderstorm,
                IsThunderstorm(weatherCode.Value))
            : metrics.MarkMissing(
                ForecastMetricName.Thunderstorm,
                thunderstormFlags & (ForecastQualityMask.ModelUnsupported | ForecastQualityMask.InvalidValue | ForecastQualityMask.MissingMetric));

        var metricSet = ForecastMetricSet.Create(
            windSpeedMs: windSpeed,
            windGustMs: windGust,
            windDirectionDeg: windDirection,
            temperatureC: temperature,
            relativeHumidityPct: humidity,
            surfacePressureHpa: pressure,
            cloudCoverPct: cloudCover,
            precipitationMmPerHour: precipitation,
            capeJkg: cape,
            visibilityM: visibility,
            weatherCode: weatherCode,
            thunderstorm: thunderstorm);

        return metrics.Complete(metricSet);
    }

    private static NormalizedPoint MapMarinePoint(OpenMeteoHourlyData hourly, int index)
    {
        var metrics = new MetricAccumulator(MarineMetrics);
        var waveHeight = metrics.ReadDouble(
            ForecastMetricName.WaveHeightM,
            hourly.WaveHeightM,
            index,
            IsNonNegative);
        var wavePeriod = metrics.ReadDouble(
            ForecastMetricName.WavePeriodS,
            hourly.WavePeriodS,
            index,
            IsNonNegative);
        var wavePeakPeriod = ReadOptionalDouble(
            hourly.WavePeakPeriodS,
            index,
            IsNonNegative);
        var waveDirection = metrics.ReadDouble(
            ForecastMetricName.WaveDirectionDeg,
            hourly.WaveDirectionDeg,
            index,
            IsDirection,
            NormalizeDirection);
        var windWaveHeight = metrics.ReadDouble(
            ForecastMetricName.WindWaveHeightM,
            hourly.WindWaveHeightM,
            index,
            IsNonNegative);
        var windWavePeriod = metrics.ReadDouble(
            ForecastMetricName.WindWavePeriodS,
            hourly.WindWavePeriodS,
            index,
            IsNonNegative);
        var windWavePeakPeriod = ReadOptionalDouble(
            hourly.WindWavePeakPeriodS,
            index,
            IsNonNegative);
        var windWaveDirection = metrics.ReadDouble(
            ForecastMetricName.WindWaveDirectionDeg,
            hourly.WindWaveDirectionDeg,
            index,
            IsDirection,
            NormalizeDirection);
        var swellHeight = metrics.ReadDouble(
            ForecastMetricName.SwellHeightM,
            hourly.SwellHeightM,
            index,
            IsNonNegative);
        var swellPeriod = metrics.ReadDouble(
            ForecastMetricName.SwellPeriodS,
            hourly.SwellPeriodS,
            index,
            IsNonNegative);
        var swellPeakPeriod = ReadOptionalDouble(
            hourly.SwellPeakPeriodS,
            index,
            IsNonNegative);
        var swellDirection = metrics.ReadDouble(
            ForecastMetricName.SwellDirectionDeg,
            hourly.SwellDirectionDeg,
            index,
            IsDirection,
            NormalizeDirection);

        var metricSet = ForecastMetricSet.Create(
            waveHeightM: waveHeight,
            wavePeriodS: wavePeriod,
            wavePeakPeriodS: wavePeakPeriod,
            waveDirectionDeg: waveDirection,
            windWaveHeightM: windWaveHeight,
            windWavePeriodS: windWavePeriod,
            windWavePeakPeriodS: windWavePeakPeriod,
            windWaveDirectionDeg: windWaveDirection,
            swellHeightM: swellHeight,
            swellPeriodS: swellPeriod,
            swellPeakPeriodS: swellPeakPeriod,
            swellDirectionDeg: swellDirection);

        return metrics.Complete(metricSet);
    }

    private static DateTimeOffset[] ParseTimes(string?[]? rawTimes)
    {
        if (rawTimes is null || rawTimes.Length == 0)
        {
            throw Contract("The Open-Meteo hourly time array is missing or empty.");
        }

        var times = new DateTimeOffset[rawTimes.Length];
        for (var index = 0; index < rawTimes.Length; index++)
        {
            var rawTime = rawTimes[index];
            if (string.IsNullOrWhiteSpace(rawTime) ||
                !DateTimeOffset.TryParse(
                    rawTime,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsedTime))
            {
                throw Contract($"The Open-Meteo hourly time at index {index} is invalid.");
            }

            if (index > 0 && times[index - 1] >= parsedTime)
            {
                throw Contract("The Open-Meteo hourly time array must be strictly ascending.");
            }

            times[index] = parsedTime.ToUniversalTime();
        }

        return times;
    }

    private static void ValidateArrayLengths(OpenMeteoHourlyData hourly, int timeCount, ForecastDataDomain dataDomain)
    {
        var arrays = dataDomain == ForecastDataDomain.Weather
            ? new (string Name, int? Length)[]
            {
                ("wind_speed_10m", hourly.WindSpeedMs?.Length),
                ("wind_gusts_10m", hourly.WindGustMs?.Length),
                ("wind_direction_10m", hourly.WindDirectionDeg?.Length),
                ("temperature_2m", hourly.TemperatureC?.Length),
                ("relative_humidity_2m", hourly.RelativeHumidityPct?.Length),
                ("surface_pressure", hourly.SurfacePressureHpa?.Length),
                ("cloud_cover", hourly.CloudCoverPct?.Length),
                ("precipitation", hourly.PrecipitationMm?.Length),
                ("cape", hourly.CapeJkg?.Length),
                ("visibility", hourly.VisibilityM?.Length),
                ("weather_code", hourly.WeatherCode?.Length)
            }
            : new (string Name, int? Length)[]
            {
                ("wave_height", hourly.WaveHeightM?.Length),
                ("wave_period", hourly.WavePeriodS?.Length),
                ("wave_peak_period", hourly.WavePeakPeriodS?.Length),
                ("wave_direction", hourly.WaveDirectionDeg?.Length),
                ("wind_wave_height", hourly.WindWaveHeightM?.Length),
                ("wind_wave_period", hourly.WindWavePeriodS?.Length),
                ("wind_wave_peak_period", hourly.WindWavePeakPeriodS?.Length),
                ("wind_wave_direction", hourly.WindWaveDirectionDeg?.Length),
                ("swell_wave_height", hourly.SwellHeightM?.Length),
                ("swell_wave_period", hourly.SwellPeriodS?.Length),
                ("swell_wave_peak_period", hourly.SwellPeakPeriodS?.Length),
                ("swell_wave_direction", hourly.SwellDirectionDeg?.Length)
            };

        foreach (var (name, length) in arrays)
        {
            if (length.HasValue && length.Value != timeCount)
            {
                throw Contract($"The Open-Meteo '{name}' array length does not match the hourly time array.");
            }
        }
    }

    private static GeoPoint ParseGridLocation(OpenMeteoForecastResponse response)
    {
        if (!response.Latitude.HasValue || !response.Longitude.HasValue)
        {
            throw Contract("The Open-Meteo response does not contain a grid coordinate.");
        }

        try
        {
            return new GeoPoint(response.Latitude.Value, response.Longitude.Value);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw Contract("The Open-Meteo response contains an invalid grid coordinate.", exception);
        }
    }

    private static DataQuality AggregateQuality(
        IReadOnlyCollection<ForecastPoint> points,
        IReadOnlyCollection<ForecastMetricName> expectedMetrics)
    {
        var qualities = points.Select(point => point.Quality).ToArray();
        var status = qualities.Any(quality => quality.Status == ForecastQualityStatus.Invalid)
            ? ForecastQualityStatus.Invalid
            : qualities.All(quality => quality.Status == ForecastQualityStatus.Unknown)
                ? ForecastQualityStatus.Unknown
                : qualities.Any(quality => quality.Status is ForecastQualityStatus.Partial or ForecastQualityStatus.Unknown)
                    ? ForecastQualityStatus.Partial
                    : ForecastQualityStatus.Valid;
        var freshness = qualities.Any(quality => quality.Freshness == ForecastFreshness.Expired)
            ? ForecastFreshness.Expired
            : qualities.Any(quality => quality.Freshness == ForecastFreshness.Stale)
                ? ForecastFreshness.Stale
                : status == ForecastQualityStatus.Unknown
                    ? ForecastFreshness.Unknown
                    : ForecastFreshness.Fresh;
        var completeness = qualities.Length == 0
            ? 0
            : qualities.Average(quality => quality.Completeness);
        var flags = qualities.Aggregate(ForecastQualityMask.None, (current, quality) => current | quality.Flags);
        var missingMetrics = expectedMetrics
            .Where(metric => qualities.Any(quality => quality.MissingMetrics.Contains(metric)))
            .ToArray();

        return new DataQuality(status, freshness, completeness, flags, missingMetrics);
    }

    // Optional enrichment: peak-period metrics depend on a wave-spectrum model the
    // default `best_match` provider does not return nearshore. A missing value must
    // neither count against completeness nor surface as a missing metric.
    private static double? ReadOptionalDouble(
        double?[]? values,
        int index,
        Func<double, bool>? validator = null,
        Func<double, double>? normalizer = null)
    {
        if (values is null)
        {
            return null;
        }

        var rawValue = values[index];
        if (!rawValue.HasValue ||
            !double.IsFinite(rawValue.Value) ||
            (validator is not null && !validator(rawValue.Value)))
        {
            return null;
        }

        return normalizer is null ? rawValue.Value : normalizer(rawValue.Value);
    }

    private static bool IsNonNegative(double value) => value >= 0;

    private static bool IsPercentage(double value) => value is >= 0 and <= 100;

    private static bool IsDirection(double value) => value is >= 0 and <= 360;

    private static double NormalizeDirection(double value) => value == 360 ? 0 : value;

    private static bool IsThunderstorm(int weatherCode) => weatherCode is 95 or 96 or 99;

    private static ProviderContractException Contract(string message, Exception? innerException = null) =>
        new("open-meteo", message, innerException);

    private sealed class MetricAccumulator(IReadOnlyCollection<ForecastMetricName> expectedMetrics)
    {
        private readonly HashSet<ForecastMetricName> _expectedMetrics = expectedMetrics.ToHashSet();
        private readonly Dictionary<ForecastMetricName, ForecastQualityMask> _flags = [];
        private readonly HashSet<ForecastMetricName> _missing = [];
        private int _presentCount;
        private ForecastQualityMask _overallFlags;

        public double? ReadDouble(
            ForecastMetricName metric,
            double?[]? values,
            int index,
            Func<double, bool>? validator = null,
            Func<double, double>? normalizer = null)
        {
            if (values is null)
            {
                return MarkMissing<double>(metric, ForecastQualityMask.ModelUnsupported);
            }

            var rawValue = values[index];
            if (!rawValue.HasValue)
            {
                return MarkMissing<double>(metric, ForecastQualityMask.None);
            }

            if (!double.IsFinite(rawValue.Value) || (validator is not null && !validator(rawValue.Value)))
            {
                return MarkMissing<double>(metric, ForecastQualityMask.InvalidValue);
            }

            var value = normalizer is null ? rawValue.Value : normalizer(rawValue.Value);
            MarkPresent(metric, ForecastQualityMask.None);
            return value;
        }

        public int? ReadInt(
            ForecastMetricName metric,
            int?[]? values,
            int index,
            Func<int, bool>? validator = null)
        {
            if (values is null)
            {
                return MarkMissing<int>(metric, ForecastQualityMask.ModelUnsupported);
            }

            var rawValue = values[index];
            if (!rawValue.HasValue)
            {
                return MarkMissing<int>(metric, ForecastQualityMask.None);
            }

            if (validator is not null && !validator(rawValue.Value))
            {
                return MarkMissing<int>(metric, ForecastQualityMask.InvalidValue);
            }

            MarkPresent(metric, ForecastQualityMask.None);
            return rawValue;
        }

        public bool? MarkPresent(ForecastMetricName metric, bool value)
        {
            MarkPresent(metric, ForecastQualityMask.None);
            return value;
        }

        public T? MarkMissing<T>(ForecastMetricName metric, ForecastQualityMask reason)
            where T : struct
        {
            MarkMissing(metric, reason);
            return null;
        }

        public bool? MarkMissing(ForecastMetricName metric, ForecastQualityMask reason)
        {
            MarkMissingCore(metric, reason);
            return null;
        }

        public ForecastQualityMask GetFlags(ForecastMetricName metric) =>
            _flags.TryGetValue(metric, out var flags) ? flags : ForecastQualityMask.None;

        public NormalizedPoint Complete(ForecastMetricSet metricSet)
        {
            var status = _overallFlags.HasFlag(ForecastQualityMask.InvalidValue)
                ? ForecastQualityStatus.Invalid
                : _presentCount == 0
                    ? ForecastQualityStatus.Unknown
                    : _missing.Count > 0
                        ? ForecastQualityStatus.Partial
                        : ForecastQualityStatus.Valid;
            var freshness = status == ForecastQualityStatus.Unknown
                ? ForecastFreshness.Unknown
                : ForecastFreshness.Fresh;
            var quality = new DataQuality(
                status,
                freshness,
                (double)_presentCount / _expectedMetrics.Count,
                _overallFlags,
                _missing);

            return new NormalizedPoint(metricSet, quality, _flags);
        }

        private void MarkPresent(ForecastMetricName metric, ForecastQualityMask flags)
        {
            _presentCount++;
            _flags[metric] = flags;
            _overallFlags |= flags;
        }

        private void MarkMissingCore(ForecastMetricName metric, ForecastQualityMask reason)
        {
            _missing.Add(metric);
            var flags = reason | ForecastQualityMask.MissingMetric;
            _flags[metric] = flags;
            _overallFlags |= flags;
        }
    }

    private sealed record NormalizedPoint(
        ForecastMetricSet Metrics,
        DataQuality Quality,
        IReadOnlyDictionary<ForecastMetricName, ForecastQualityMask> SourceFlags)
    {
        public ForecastQualityMask GetSourceFlags(ForecastMetricName metric) =>
            SourceFlags.TryGetValue(metric, out var flags) ? flags : ForecastQualityMask.None;
    }
}
