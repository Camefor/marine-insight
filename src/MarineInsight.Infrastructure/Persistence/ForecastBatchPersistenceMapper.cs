using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Persistence.Entities;

namespace MarineInsight.Infrastructure.Persistence;

internal static class ForecastBatchPersistenceMapper
{
    public static ForecastBatchEntity ToEntity(Guid locationId, ForecastBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("Location ID is required.", nameof(locationId));
        }

        var gridLocation = batch.GridLocation;
        var entity = new ForecastBatchEntity
        {
            Id = batch.BatchId,
            LocationId = locationId,
            ProviderCode = batch.Provider.ProviderCode,
            DataDomain = (short)batch.DataDomain,
            EndpointCode = batch.DataDomain.ToString(),
            ModelCode = batch.Provider.SourceModel,
            IssuedAtUtc = batch.IssuedAtUtc,
            FetchedAtUtc = batch.FetchedAtUtc,
            RangeStartUtc = batch.Range.StartUtc,
            RangeEndUtc = batch.Range.EndUtc,
            QualityStatus = (short)batch.Quality.Status,
            Freshness = (short)batch.Quality.Freshness,
            QualityFlags = (int)batch.Quality.Flags,
            Completeness = batch.Quality.Completeness,
            RequestedLatitude = (decimal)batch.RequestedLocation.Latitude,
            RequestedLongitude = (decimal)batch.RequestedLocation.Longitude,
            GridLatitude = gridLocation is null ? null : (decimal?)gridLocation.Value.Latitude,
            GridLongitude = gridLocation is null ? null : (decimal?)gridLocation.Value.Longitude
        };

        foreach (var point in batch.Points)
        {
            // ForecastPoint has no storage identity in the domain; the persistence identity
            // is generated here so metric-level source rows can reference the point.
            var pointEntity = new ForecastPointEntity
            {
                Id = Guid.NewGuid(),
                BatchId = batch.BatchId,
                ForecastTimeUtc = point.ForecastTimeUtc,
                WindSpeedMs = point.Metrics.WindSpeedMs,
                WindGustMs = point.Metrics.WindGustMs,
                WindDirectionDeg = point.Metrics.WindDirectionDeg,
                TemperatureC = point.Metrics.TemperatureC,
                RelativeHumidityPct = point.Metrics.RelativeHumidityPct,
                SurfacePressureHpa = point.Metrics.SurfacePressureHpa,
                CloudCoverPct = point.Metrics.CloudCoverPct,
                PrecipitationMmPerHour = point.Metrics.PrecipitationMmPerHour,
                CapeJkg = point.Metrics.CapeJkg,
                VisibilityM = point.Metrics.VisibilityM,
                WeatherCode = point.Metrics.WeatherCode,
                Thunderstorm = point.Metrics.Thunderstorm,
                WaveHeightM = point.Metrics.WaveHeightM,
                WavePeriodS = point.Metrics.WavePeriodS,
                WavePeakPeriodS = point.Metrics.WavePeakPeriodS,
                WaveDirectionDeg = point.Metrics.WaveDirectionDeg,
                WindWaveHeightM = point.Metrics.WindWaveHeightM,
                WindWavePeriodS = point.Metrics.WindWavePeriodS,
                WindWavePeakPeriodS = point.Metrics.WindWavePeakPeriodS,
                WindWaveDirectionDeg = point.Metrics.WindWaveDirectionDeg,
                SwellHeightM = point.Metrics.SwellHeightM,
                SwellPeriodS = point.Metrics.SwellPeriodS,
                SwellPeakPeriodS = point.Metrics.SwellPeakPeriodS,
                SwellDirectionDeg = point.Metrics.SwellDirectionDeg,
                SeaTemperatureC = point.Metrics.SeaTemperatureC,
                CurrentSpeedMs = point.Metrics.CurrentSpeedMs,
                CurrentDirectionDeg = point.Metrics.CurrentDirectionDeg,
                TideHeightM = point.Metrics.TideHeightM,
                TideType = point.Metrics.TideType is { } tideType ? (short)tideType : null,
                QualityStatus = (short)point.Quality.Status,
                Freshness = (short)point.Quality.Freshness,
                MissingMetricsMask = ToMissingMetricsMask(point.Quality.MissingMetrics),
                QualityFlags = (int)point.Quality.Flags,
                Completeness = point.Quality.Completeness
            };

            foreach (var source in point.MetricSources)
            {
                pointEntity.Sources.Add(new ForecastPointSourceEntity
                {
                    ForecastPointId = pointEntity.Id,
                    Metric = (short)source.Metric,
                    ProviderCode = source.Provider.ProviderCode,
                    SourceModel = source.Provider.SourceModel,
                    BatchId = source.BatchId,
                    ForecastTimeUtc = source.ForecastTimeUtc,
                    QualityStatus = (short)source.QualityStatus,
                    Freshness = (short)source.Freshness,
                    QualityFlags = (int)source.QualityFlags
                });
            }

            entity.Points.Add(pointEntity);
        }

        return entity;
    }

    public static ForecastBatch ToDomain(ForecastBatchEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var provider = new ProviderIdentity(
            entity.ProviderCode,
            entity.ModelCode ?? throw new InvalidOperationException(
                $"Forecast batch '{entity.Id}' does not have a source model."));
        var range = ToRange(entity.RangeStartUtc, entity.RangeEndUtc, entity.Id);
        var points = entity.Points
            .OrderBy(point => point.ForecastTimeUtc)
            .Select(point => ToDomainPoint(point))
            .ToArray();
        var missingMetrics = points
            .SelectMany(point => point.Quality.MissingMetrics)
            .Distinct()
            .OrderBy(metric => metric)
            .ToArray();

        return new ForecastBatch(
            entity.Id,
            ToDataDomain(entity.DataDomain, entity.Id),
            provider,
            new GeoPoint((double)entity.RequestedLatitude, (double)entity.RequestedLongitude),
            ToGridLocation(entity),
            entity.IssuedAtUtc ?? throw new InvalidOperationException(
                $"Forecast batch '{entity.Id}' does not have an issued timestamp."),
            entity.FetchedAtUtc,
            range,
            points,
            new DataQuality(
                ToQualityStatus(entity.QualityStatus, nameof(entity.QualityStatus), entity.Id),
                ToFreshness(entity.Freshness, nameof(entity.Freshness), entity.Id),
                entity.Completeness,
                (ForecastQualityMask)entity.QualityFlags,
                missingMetrics));
    }

    private static ForecastPoint ToDomainPoint(ForecastPointEntity entity)
    {
        var sources = entity.Sources
            .OrderBy(source => source.Metric)
            .Select(ToMetricSource)
            .ToArray();

        return new ForecastPoint(
            entity.ForecastTimeUtc,
            ForecastMetricSet.Create(
                windSpeedMs: entity.WindSpeedMs,
                windGustMs: entity.WindGustMs,
                windDirectionDeg: entity.WindDirectionDeg,
                temperatureC: entity.TemperatureC,
                relativeHumidityPct: entity.RelativeHumidityPct,
                surfacePressureHpa: entity.SurfacePressureHpa,
                cloudCoverPct: entity.CloudCoverPct,
                precipitationMmPerHour: entity.PrecipitationMmPerHour,
                capeJkg: entity.CapeJkg,
                visibilityM: entity.VisibilityM,
                weatherCode: entity.WeatherCode,
                thunderstorm: entity.Thunderstorm,
                waveHeightM: entity.WaveHeightM,
                wavePeriodS: entity.WavePeriodS,
                wavePeakPeriodS: entity.WavePeakPeriodS,
                waveDirectionDeg: entity.WaveDirectionDeg,
                windWaveHeightM: entity.WindWaveHeightM,
                windWavePeriodS: entity.WindWavePeriodS,
                windWavePeakPeriodS: entity.WindWavePeakPeriodS,
                windWaveDirectionDeg: entity.WindWaveDirectionDeg,
                swellHeightM: entity.SwellHeightM,
                swellPeriodS: entity.SwellPeriodS,
                swellPeakPeriodS: entity.SwellPeakPeriodS,
                swellDirectionDeg: entity.SwellDirectionDeg,
                seaTemperatureC: entity.SeaTemperatureC,
                currentSpeedMs: entity.CurrentSpeedMs,
                currentDirectionDeg: entity.CurrentDirectionDeg,
                tideHeightM: entity.TideHeightM,
                tideType: ToTideType(entity.TideType, entity.Id)),
            new DataQuality(
                ToQualityStatus(entity.QualityStatus, nameof(entity.QualityStatus), entity.Id),
                ToFreshness(entity.Freshness, nameof(entity.Freshness), entity.Id),
                entity.Completeness,
                (ForecastQualityMask)entity.QualityFlags,
                ToMissingMetrics(entity.MissingMetricsMask, entity.Id)),
            sources);
    }

    private static MetricSource ToMetricSource(ForecastPointSourceEntity entity)
    {
        return new MetricSource(
            ToMetric(entity.Metric, entity.ForecastPointId),
            new ProviderIdentity(entity.ProviderCode, entity.SourceModel),
            entity.BatchId,
            entity.ForecastTimeUtc,
            ToQualityStatus(entity.QualityStatus, nameof(entity.QualityStatus), entity.BatchId),
            ToFreshness(entity.Freshness, nameof(entity.Freshness), entity.BatchId),
            (ForecastQualityMask)entity.QualityFlags);
    }

    private static ForecastRange ToRange(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        Guid batchId)
    {
        var duration = endUtc.ToUniversalTime() - startUtc.ToUniversalTime();
        var hours = duration == TimeSpan.FromHours(24)
            ? 24
            : duration == TimeSpan.FromHours(72)
                ? 72
                : duration == TimeSpan.FromHours(168)
                    ? 168
                    : throw new InvalidOperationException(
                        $"Forecast batch '{batchId}' has an unsupported range duration.");

        return new ForecastRange(startUtc, hours);
    }

    private static GeoPoint? ToGridLocation(ForecastBatchEntity entity)
    {
        if (entity.GridLatitude is null && entity.GridLongitude is null)
        {
            return null;
        }

        if (entity.GridLatitude is null || entity.GridLongitude is null)
        {
            throw new InvalidOperationException(
                $"Forecast batch '{entity.Id}' has an incomplete grid location.");
        }

        return new GeoPoint((double)entity.GridLatitude.Value, (double)entity.GridLongitude.Value);
    }

    private static ForecastDataDomain ToDataDomain(short value, Guid batchId)
    {
        if (!Enum.IsDefined(typeof(ForecastDataDomain), (int)value))
        {
            throw new InvalidOperationException($"Forecast batch '{batchId}' has an unknown data domain.");
        }

        return (ForecastDataDomain)value;
    }

    private static ForecastMetricName ToMetric(short value, Guid ownerId)
    {
        if (!Enum.IsDefined(typeof(ForecastMetricName), (int)value))
        {
            throw new InvalidOperationException($"Forecast source on '{ownerId}' has an unknown metric.");
        }

        return (ForecastMetricName)value;
    }

    private static TideType? ToTideType(short? value, Guid pointId)
    {
        if (value is null)
        {
            return null;
        }

        if (!Enum.IsDefined(typeof(TideType), (int)value.Value))
        {
            throw new InvalidOperationException($"Forecast point '{pointId}' has an unknown tide type.");
        }

        return (TideType)value.Value;
    }

    private static ForecastQualityStatus ToQualityStatus(short value, string propertyName, Guid ownerId)
    {
        if (!Enum.IsDefined(typeof(ForecastQualityStatus), (int)value))
        {
            throw new InvalidOperationException($"'{propertyName}' on '{ownerId}' has an unknown quality status.");
        }

        return (ForecastQualityStatus)value;
    }

    private static ForecastFreshness ToFreshness(short value, string propertyName, Guid ownerId)
    {
        if (!Enum.IsDefined(typeof(ForecastFreshness), (int)value))
        {
            throw new InvalidOperationException($"'{propertyName}' on '{ownerId}' has an unknown freshness value.");
        }

        return (ForecastFreshness)value;
    }

    private static long ToMissingMetricsMask(IEnumerable<ForecastMetricName> metrics)
    {
        var mask = 0L;
        foreach (var metric in metrics)
        {
            mask |= 1L << (int)metric;
        }

        return mask;
    }

    private static ForecastMetricName[] ToMissingMetrics(long mask, Guid pointId)
    {
        if (mask < 0)
        {
            throw new InvalidOperationException($"Forecast point '{pointId}' has an invalid missing metric mask.");
        }

        var knownMask = Enum.GetValues<ForecastMetricName>()
            .Aggregate(0L, (current, metric) => current | (1L << (int)metric));
        if ((mask & ~knownMask) != 0)
        {
            throw new InvalidOperationException($"Forecast point '{pointId}' has an unknown missing metric bit.");
        }

        return Enum.GetValues<ForecastMetricName>()
            .Where(metric => (mask & (1L << (int)metric)) != 0)
            .ToArray();
    }
}
