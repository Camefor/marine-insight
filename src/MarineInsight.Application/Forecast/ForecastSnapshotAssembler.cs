using System.Diagnostics.CodeAnalysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Forecast;

/// <summary>
/// Combines independent source batches into the immutable input consumed by analysis.
/// </summary>
public sealed class ForecastSnapshotAssembler
{
    [SuppressMessage(
        "Performance",
        "CA1822",
        Justification = "The assembler remains an instance service so it can be composed by the Application layer and extended with policies.")]
    public ForecastSnapshot Assemble(
        IEnumerable<ForecastBatch> batches,
        ForecastRange range,
        ForecastSnapshotAssemblyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(batches);

        options ??= new ForecastSnapshotAssemblyOptions();
        options.Validate();

        var inputBatches = batches.ToArray();
        if (inputBatches.Length == 0)
        {
            throw new ArgumentException("At least one forecast batch is required.", nameof(batches));
        }

        if (inputBatches.Any(batch => batch is null))
        {
            throw new ArgumentException("Forecast batches cannot contain null values.", nameof(batches));
        }

        ValidateRange(range);

        var selectedBatches = SelectBatches(inputBatches, options);
        ValidateLocation(selectedBatches);

        var timeline = selectedBatches
            .SelectMany(batch => batch.Points)
            .Where(point => range.Contains(point.ForecastTimeUtc))
            .Select(point => point.ForecastTimeUtc)
            .Distinct()
            .OrderBy(time => time)
            .ToArray();

        if (timeline.Length == 0)
        {
            throw new ArgumentException("The supplied forecast batches contain no points in the requested range.", nameof(range));
        }

        var points = timeline
            .Select(time => BuildPoint(time, selectedBatches, range, options))
            .ToArray();
        var sourceReferences = selectedBatches
            .Select(SourceBatchReference.FromBatch)
            .ToArray();

        return new ForecastSnapshot(
            Guid.NewGuid(),
            selectedBatches[0].RequestedLocation,
            range,
            points,
            sourceReferences,
            AggregateQuality(points));
    }

    private static ForecastSnapshotPoint BuildPoint(
        DateTimeOffset targetTime,
        IReadOnlyList<ForecastBatch> batches,
        ForecastRange range,
        ForecastSnapshotAssemblyOptions options)
    {
        var matches = batches
            .Select(batch => FindNearest(batch, targetTime, range, options.MaximumAlignmentGap))
            .ToArray();
        var candidates = new List<MetricCandidate>();

        foreach (var match in matches)
        {
            if (match.Point is null)
            {
                continue;
            }

            foreach (var metric in match.Point.Metrics.GetPresentMetrics())
            {
                var source = match.Point.MetricSources.Single(candidate => candidate.Metric == metric);
                candidates.Add(new MetricCandidate(metric, match.Point.Metrics, source));
            }
        }

        var mergedMetrics = MergeMetrics(candidates, options.PreferredMetricProviders);
        var missingDomains = matches
            .Where(match => match.Point is null)
            .Select(match => match.Batch.DataDomain)
            .Distinct()
            .ToArray();
        var hasTimeGap = missingDomains.Length > 0 || matches.Any(match =>
            match.Distance.HasValue && match.Distance.Value > TimeSpan.Zero);
        var quality = BuildPointQuality(matches, missingDomains, hasTimeGap);

        return new ForecastSnapshotPoint(
            targetTime,
            mergedMetrics.Metrics,
            quality,
            mergedMetrics.Sources);
    }

    private static List<ForecastBatch> SelectBatches(
        IReadOnlyCollection<ForecastBatch> batches,
        ForecastSnapshotAssemblyOptions options)
    {
        var selected = new List<ForecastBatch>();
        foreach (var group in batches.GroupBy(batch => batch.DataDomain).OrderBy(group => group.Key))
        {
            if (!Enum.IsDefined(group.Key))
            {
                throw new ArgumentException($"Unsupported forecast data domain '{group.Key}'.", nameof(batches));
            }

            var candidates = group.ToArray();
            if (candidates.Length == 1)
            {
                selected.Add(candidates[0]);
                continue;
            }

            if (!options.PreferredBatchProviders.TryGetValue(group.Key, out var preferredProvider))
            {
                throw new InvalidOperationException(
                    $"Multiple {group.Key} batches require an explicit preferred batch provider.");
            }

            var matches = candidates
                .Where(batch => batch.Provider == preferredProvider)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Preferred provider '{preferredProvider.ProviderCode}/{preferredProvider.SourceModel}' does not identify exactly one {group.Key} batch.");
            }

            selected.Add(matches[0]);
        }

        return selected;
    }

    private static void ValidateLocation(List<ForecastBatch> batches)
    {
        var requestedLocation = batches[0].RequestedLocation;
        if (batches.Any(batch => batch.RequestedLocation != requestedLocation))
        {
            throw new ArgumentException("All forecast batches must use the same requested location.", nameof(batches));
        }
    }

    private static BatchMatch FindNearest(
        ForecastBatch batch,
        DateTimeOffset targetTime,
        ForecastRange range,
        TimeSpan maximumAlignmentGap)
    {
        var nearest = batch.Points
            .Where(point => range.Contains(point.ForecastTimeUtc))
            .Select(point => new NearestPoint(point, DistanceBetween(point.ForecastTimeUtc, targetTime)))
            .Where(candidate => candidate.Distance <= maximumAlignmentGap)
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Point.ForecastTimeUtc)
            .FirstOrDefault();

        return new BatchMatch(batch, nearest?.Point, nearest?.Distance);
    }

    private static SnapshotQuality BuildPointQuality(
        IReadOnlyCollection<BatchMatch> matches,
        ForecastDataDomain[] missingDomains,
        bool hasTimeGap)
    {
        var matched = matches.Where(match => match.Point is not null).ToArray();
        var pointQualities = matched.Select(match => match.Point!.Quality).ToArray();
        var batchQualities = matched.Select(match => match.Batch.Quality).ToArray();
        var allQualities = pointQualities.Concat(batchQualities).ToArray();
        var flags = allQualities.Aggregate(
            ForecastQualityMask.None,
            (current, quality) => current | quality.Flags);

        if (hasTimeGap)
        {
            flags |= ForecastQualityMask.TimeGap;
        }

        if (allQualities.Any(quality => quality.Freshness == ForecastFreshness.Stale))
        {
            flags |= ForecastQualityMask.StaleData;
        }

        if (allQualities.Any(quality => quality.Freshness == ForecastFreshness.Expired))
        {
            flags |= ForecastQualityMask.ExpiredData;
        }

        var status = GetStatus(pointQualities, batchQualities, hasTimeGap);
        var freshness = GetFreshness(allQualities);
        var completeness = matches.Count == 0
            ? 0
            : matches.Sum(match => match.Point is null
                ? 0
                : Math.Min(match.Point.Quality.Completeness, match.Batch.Quality.Completeness)) / matches.Count;
        var missingMetrics = allQualities
            .SelectMany(quality => quality.MissingMetrics)
            .Distinct()
            .ToArray();

        return new SnapshotQuality(status, freshness, completeness, flags, missingMetrics, missingDomains);
    }

    private static SnapshotQuality AggregateQuality(IReadOnlyCollection<ForecastSnapshotPoint> points)
    {
        var qualities = points.Select(point => point.Quality).ToArray();
        var status = qualities.Any(quality => quality.Status == ForecastQualityStatus.Invalid)
            ? ForecastQualityStatus.Invalid
            : qualities.All(quality => quality.Status == ForecastQualityStatus.Unknown)
                ? ForecastQualityStatus.Unknown
                : qualities.Any(quality => quality.Status == ForecastQualityStatus.Stale)
                    ? ForecastQualityStatus.Stale
                    : qualities.Any(quality => quality.Status is ForecastQualityStatus.Partial or ForecastQualityStatus.Unknown)
                        ? ForecastQualityStatus.Partial
                        : ForecastQualityStatus.Valid;
        var freshness = GetFreshness(qualities.Select(quality => new DataQuality(
            quality.Status,
            quality.Freshness,
            quality.Completeness,
            quality.Flags,
            quality.MissingMetrics)).ToArray());
        var flags = qualities.Aggregate(
            ForecastQualityMask.None,
            (current, quality) => current | quality.Flags);
        var missingMetrics = qualities
            .SelectMany(quality => quality.MissingMetrics)
            .Distinct()
            .ToArray();
        var missingDomains = qualities
            .SelectMany(quality => quality.MissingDomains)
            .Distinct()
            .ToArray();

        return new SnapshotQuality(
            status,
            freshness,
            qualities.Length == 0 ? 0 : qualities.Average(quality => quality.Completeness),
            flags,
            missingMetrics,
            missingDomains);
    }

    private static ForecastQualityStatus GetStatus(
        IReadOnlyCollection<DataQuality> pointQualities,
        IReadOnlyCollection<DataQuality> batchQualities,
        bool hasTimeGap)
    {
        var qualities = pointQualities.Concat(batchQualities).ToArray();
        if (qualities.Length == 0 || qualities.All(quality => quality.Status == ForecastQualityStatus.Unknown))
        {
            return ForecastQualityStatus.Unknown;
        }

        if (qualities.Any(quality => quality.Status == ForecastQualityStatus.Invalid))
        {
            return ForecastQualityStatus.Invalid;
        }

        if (qualities.Any(quality =>
                quality.Status == ForecastQualityStatus.Stale ||
                quality.Freshness is ForecastFreshness.Stale or ForecastFreshness.Expired))
        {
            return ForecastQualityStatus.Stale;
        }

        if (hasTimeGap || qualities.Any(quality => quality.Status is ForecastQualityStatus.Partial or ForecastQualityStatus.Unknown))
        {
            return ForecastQualityStatus.Partial;
        }

        return ForecastQualityStatus.Valid;
    }

    private static ForecastFreshness GetFreshness(IEnumerable<DataQuality> qualities)
    {
        var qualityArray = qualities.ToArray();
        if (qualityArray.Length == 0 || qualityArray.All(quality => quality.Freshness == ForecastFreshness.Unknown))
        {
            return ForecastFreshness.Unknown;
        }

        if (qualityArray.Any(quality => quality.Freshness == ForecastFreshness.Expired))
        {
            return ForecastFreshness.Expired;
        }

        if (qualityArray.Any(quality => quality.Freshness == ForecastFreshness.Stale))
        {
            return ForecastFreshness.Stale;
        }

        return ForecastFreshness.Fresh;
    }

    private static TimeSpan DistanceBetween(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left - right : right - left;

    private static void ValidateRange(ForecastRange range)
    {
        if (range.Hours is not (24 or 72 or 168))
        {
            throw new ArgumentOutOfRangeException(nameof(range), "Forecast range must be 24, 72, or 168 hours.");
        }
    }

    private sealed record BatchMatch(ForecastBatch Batch, ForecastPoint? Point, TimeSpan? Distance);

    private sealed record NearestPoint(ForecastPoint Point, TimeSpan Distance);

    private sealed record MetricCandidate(
        ForecastMetricName Metric,
        ForecastMetricSet Metrics,
        MetricSource Source);

    private sealed record MergedMetrics(
        ForecastMetricSet Metrics,
        IReadOnlyList<MetricSource> Sources);

    private sealed class SnapshotMetricAccumulator
    {
        private readonly Dictionary<ForecastMetricName, object> _values = [];
        private readonly Dictionary<ForecastMetricName, MetricSource> _sources = [];

        public void Add(MetricCandidate candidate)
        {
            if (candidate.Source.Metric != candidate.Metric)
            {
                throw new InvalidOperationException("A metric candidate source does not match its metric.");
            }

            if (!_values.TryAdd(candidate.Metric, ReadMetric(candidate.Metrics, candidate.Metric)))
            {
                throw new InvalidOperationException($"Metric '{candidate.Metric}' was selected more than once.");
            }

            _sources.Add(candidate.Metric, candidate.Source);
        }

        public ForecastMetricSet BuildMetricSet() => ForecastMetricSet.Create(
            windSpeedMs: ReadDouble(ForecastMetricName.WindSpeedMs),
            windGustMs: ReadDouble(ForecastMetricName.WindGustMs),
            windDirectionDeg: ReadDouble(ForecastMetricName.WindDirectionDeg),
            temperatureC: ReadDouble(ForecastMetricName.TemperatureC),
            relativeHumidityPct: ReadDouble(ForecastMetricName.RelativeHumidityPct),
            surfacePressureHpa: ReadDouble(ForecastMetricName.SurfacePressureHpa),
            cloudCoverPct: ReadDouble(ForecastMetricName.CloudCoverPct),
            precipitationMmPerHour: ReadDouble(ForecastMetricName.PrecipitationMmPerHour),
            capeJkg: ReadDouble(ForecastMetricName.CapeJkg),
            visibilityM: ReadDouble(ForecastMetricName.VisibilityM),
            weatherCode: ReadInt(ForecastMetricName.WeatherCode),
            thunderstorm: ReadBool(ForecastMetricName.Thunderstorm),
            waveHeightM: ReadDouble(ForecastMetricName.WaveHeightM),
            wavePeriodS: ReadDouble(ForecastMetricName.WavePeriodS),
            wavePeakPeriodS: ReadDouble(ForecastMetricName.WavePeakPeriodS),
            waveDirectionDeg: ReadDouble(ForecastMetricName.WaveDirectionDeg),
            windWaveHeightM: ReadDouble(ForecastMetricName.WindWaveHeightM),
            windWavePeriodS: ReadDouble(ForecastMetricName.WindWavePeriodS),
            windWavePeakPeriodS: ReadDouble(ForecastMetricName.WindWavePeakPeriodS),
            windWaveDirectionDeg: ReadDouble(ForecastMetricName.WindWaveDirectionDeg),
            swellHeightM: ReadDouble(ForecastMetricName.SwellHeightM),
            swellPeriodS: ReadDouble(ForecastMetricName.SwellPeriodS),
            swellPeakPeriodS: ReadDouble(ForecastMetricName.SwellPeakPeriodS),
            swellDirectionDeg: ReadDouble(ForecastMetricName.SwellDirectionDeg),
            seaTemperatureC: ReadDouble(ForecastMetricName.SeaTemperatureC),
            currentSpeedMs: ReadDouble(ForecastMetricName.CurrentSpeedMs),
            currentDirectionDeg: ReadDouble(ForecastMetricName.CurrentDirectionDeg),
            tideHeightM: ReadDouble(ForecastMetricName.TideHeightM),
            tideType: ReadTideType(ForecastMetricName.TideType));

        public IReadOnlyList<MetricSource> Sources => _sources
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value)
            .ToArray();

        private double? ReadDouble(ForecastMetricName metric) =>
            _values.TryGetValue(metric, out var value) ? (double)value : null;

        private int? ReadInt(ForecastMetricName metric) =>
            _values.TryGetValue(metric, out var value) ? (int)value : null;

        private bool? ReadBool(ForecastMetricName metric) =>
            _values.TryGetValue(metric, out var value) ? (bool)value : null;

        private TideType? ReadTideType(ForecastMetricName metric) =>
            _values.TryGetValue(metric, out var value) ? (TideType)value : null;

        private static object ReadMetric(ForecastMetricSet metrics, ForecastMetricName metric) => metric switch
        {
            ForecastMetricName.WindSpeedMs => metrics.WindSpeedMs!.Value,
            ForecastMetricName.WindGustMs => metrics.WindGustMs!.Value,
            ForecastMetricName.WindDirectionDeg => metrics.WindDirectionDeg!.Value,
            ForecastMetricName.TemperatureC => metrics.TemperatureC!.Value,
            ForecastMetricName.RelativeHumidityPct => metrics.RelativeHumidityPct!.Value,
            ForecastMetricName.SurfacePressureHpa => metrics.SurfacePressureHpa!.Value,
            ForecastMetricName.CloudCoverPct => metrics.CloudCoverPct!.Value,
            ForecastMetricName.PrecipitationMmPerHour => metrics.PrecipitationMmPerHour!.Value,
            ForecastMetricName.CapeJkg => metrics.CapeJkg!.Value,
            ForecastMetricName.VisibilityM => metrics.VisibilityM!.Value,
            ForecastMetricName.WeatherCode => metrics.WeatherCode!.Value,
            ForecastMetricName.Thunderstorm => metrics.Thunderstorm!.Value,
            ForecastMetricName.WaveHeightM => metrics.WaveHeightM!.Value,
            ForecastMetricName.WavePeriodS => metrics.WavePeriodS!.Value,
            ForecastMetricName.WavePeakPeriodS => metrics.WavePeakPeriodS!.Value,
            ForecastMetricName.WaveDirectionDeg => metrics.WaveDirectionDeg!.Value,
            ForecastMetricName.WindWaveHeightM => metrics.WindWaveHeightM!.Value,
            ForecastMetricName.WindWavePeriodS => metrics.WindWavePeriodS!.Value,
            ForecastMetricName.WindWavePeakPeriodS => metrics.WindWavePeakPeriodS!.Value,
            ForecastMetricName.WindWaveDirectionDeg => metrics.WindWaveDirectionDeg!.Value,
            ForecastMetricName.SwellHeightM => metrics.SwellHeightM!.Value,
            ForecastMetricName.SwellPeriodS => metrics.SwellPeriodS!.Value,
            ForecastMetricName.SwellPeakPeriodS => metrics.SwellPeakPeriodS!.Value,
            ForecastMetricName.SwellDirectionDeg => metrics.SwellDirectionDeg!.Value,
            ForecastMetricName.SeaTemperatureC => metrics.SeaTemperatureC!.Value,
            ForecastMetricName.CurrentSpeedMs => metrics.CurrentSpeedMs!.Value,
            ForecastMetricName.CurrentDirectionDeg => metrics.CurrentDirectionDeg!.Value,
            ForecastMetricName.TideHeightM => metrics.TideHeightM!.Value,
            ForecastMetricName.TideType => metrics.TideType!.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unsupported forecast metric.")
        };
    }

    private static MergedMetrics MergeMetrics(
        IReadOnlyCollection<MetricCandidate> candidates,
        IReadOnlyDictionary<ForecastMetricName, ProviderIdentity> preferredProviders)
    {
        var accumulator = new SnapshotMetricAccumulator();
        foreach (var group in candidates.GroupBy(candidate => candidate.Metric).OrderBy(group => group.Key))
        {
            var groupCandidates = group.ToArray();
            var selected = SelectMetricCandidate(group.Key, groupCandidates, preferredProviders);
            accumulator.Add(selected);
        }

        return new MergedMetrics(accumulator.BuildMetricSet(), accumulator.Sources);
    }

    private static MetricCandidate SelectMetricCandidate(
        ForecastMetricName metric,
        IReadOnlyCollection<MetricCandidate> candidates,
        IReadOnlyDictionary<ForecastMetricName, ProviderIdentity> preferredProviders)
    {
        if (candidates.Count == 1)
        {
            return candidates.Single();
        }

        if (!preferredProviders.TryGetValue(metric, out var preferredProvider))
        {
            throw new InvalidOperationException(
                $"Metric '{metric}' has multiple source providers and requires an explicit selection policy.");
        }

        var matches = candidates
            .Where(candidate => candidate.Source.Provider == preferredProvider)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Preferred provider '{preferredProvider.ProviderCode}/{preferredProvider.SourceModel}' does not identify exactly one source for metric '{metric}'.");
        }

        return matches[0];
    }
}
