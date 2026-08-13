using MarineInsight.Application.Analysis;
using MarineInsight.Application.Forecast;
using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Tests;

internal static class AnalysisTestFactory
{
    internal static readonly DateTimeOffset StartUtc = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);
    internal static readonly GeoPoint Location = new(30.194, 122.687);
    internal static readonly ForecastRange Range = new(StartUtc, 24);
    internal const string AlgorithmVersion = "marine-score-1.0.0";

    private static readonly ProviderIdentity WeatherProvider = new("open-meteo", "weather-v1");
    private static readonly ProviderIdentity MarineProvider = new("open-meteo", "marine-v1");

    internal static MarineAnalysisQueryResult CreateResult(
        IEnumerable<ActivityType> activities,
        double? score = 72,
        RiskLevel riskLevel = RiskLevel.Good,
        double confidence = 0.85,
        IReadOnlyList<RiskContribution>? contributions = null,
        IReadOnlyList<RecommendationWindow>? windows = null,
        IReadOnlyList<HourlyMarineAssessment>? assessments = null,
        SnapshotQuality? snapshotQuality = null)
    {
        var weatherBatch = CreateBatch(
            ForecastDataDomain.Weather,
            WeatherProvider,
            ForecastMetricSet.Create(windSpeedMs: 4));
        var marineBatch = CreateBatch(
            ForecastDataDomain.Marine,
            MarineProvider,
            ForecastMetricSet.Create(waveHeightM: 0.8));
        var sourceBatches = new[]
        {
            SourceBatchReference.FromBatch(weatherBatch),
            SourceBatchReference.FromBatch(marineBatch)
        };
        var quality = snapshotQuality ?? new SnapshotQuality(
            ForecastQualityStatus.Valid,
            ForecastFreshness.Fresh,
            1.0);

        var activityList = activities as IReadOnlyList<ActivityType> ?? activities.ToArray();
        var resolvedAssessments = assessments ?? new[]
        {
            new HourlyMarineAssessment(
                StartUtc,
                score,
                riskLevel,
                confidence,
                AlgorithmVersion,
                contributions ?? [],
                activityList.Select(activity => new ActivityMarineAssessment(
                    activity,
                    StartUtc,
                    score,
                    riskLevel,
                    confidence,
                    AlgorithmVersion)))
        };

        var cacheIdentity = MarineAnalysisCacheIdentity.Create(
            sourceBatches,
            activityList,
            AlgorithmVersion);

        return new MarineAnalysisQueryResult(
            new MarineAnalysisQuery(Location, Range, activities: activityList),
            CreateSnapshot(sourceBatches, quality),
            resolvedAssessments,
            windows ?? [],
            cacheIdentity,
            new ForecastCacheResult(weatherBatch, ForecastCacheResultKind.Provider, null),
            new ForecastCacheResult(marineBatch, ForecastCacheResultKind.Provider, null));
    }

    internal static RiskContribution Risk(
        string code,
        RiskSeverity severity,
        double penalty,
        double? actual = null,
        double? threshold = null,
        string message = "测试风险") =>
        new(code, RiskContributionKind.BasePenalty, severity, "metric", actual, threshold, penalty, message);

    private static ForecastSnapshot CreateSnapshot(
        SourceBatchReference[] sourceBatches,
        SnapshotQuality quality)
    {
        var point = new ForecastSnapshotPoint(
            StartUtc,
            ForecastMetricSet.Create(windSpeedMs: 4, waveHeightM: 0.8),
            quality,
            [
                new MetricSource(
                    ForecastMetricName.WindSpeedMs,
                    WeatherProvider,
                    sourceBatches[0].BatchId,
                    StartUtc,
                    ForecastQualityStatus.Valid,
                    ForecastFreshness.Fresh),
                new MetricSource(
                    ForecastMetricName.WaveHeightM,
                    MarineProvider,
                    sourceBatches[1].BatchId,
                    StartUtc,
                    ForecastQualityStatus.Valid,
                    ForecastFreshness.Fresh)
            ]);

        return new ForecastSnapshot(
            Guid.NewGuid(),
            Location,
            Range,
            [point],
            sourceBatches,
            quality);
    }

    private static ForecastBatch CreateBatch(
        ForecastDataDomain dataDomain,
        ProviderIdentity provider,
        ForecastMetricSet metrics)
    {
        var batchId = Guid.NewGuid();
        var metric = metrics.GetPresentMetrics().Single();
        var point = new ForecastPoint(
            StartUtc,
            metrics,
            DataQuality.Valid(),
            [new MetricSource(
                metric,
                provider,
                batchId,
                StartUtc,
                ForecastQualityStatus.Valid,
                ForecastFreshness.Fresh)]);

        return new ForecastBatch(
            batchId,
            dataDomain,
            provider,
            Location,
            null,
            StartUtc.AddHours(-1),
            StartUtc.AddHours(-1),
            Range,
            [point],
            DataQuality.Valid());
    }
}
