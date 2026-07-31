using System.Globalization;
using MarineInsight.Application.Analysis;
using MarineInsight.Application.Errors;
using MarineInsight.Application.Forecast;
using MarineInsight.Application.Locations;
using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;
using MarineInsight.Domain.Location;

namespace MarineInsight.Web.Components.Features.Dashboard;

public sealed class DashboardQuerySession : IDisposable
{
    private readonly MarineAnalysisQueryService _analysisQueryService;
    private readonly LocationQueryService _locationQueryService;
    private CancellationTokenSource? _activeRequest;
    private long _requestVersion;

    public DashboardQuerySession(
        MarineAnalysisQueryService analysisQueryService,
        LocationQueryService locationQueryService)
    {
        ArgumentNullException.ThrowIfNull(analysisQueryService);
        ArgumentNullException.ThrowIfNull(locationQueryService);

        _analysisQueryService = analysisQueryService;
        _locationQueryService = locationQueryService;
        ForecastStartUtc = RoundToNextUtcHour(DateTimeOffset.UtcNow).UtcDateTime;
    }

    public string SearchText { get; set; } = "东极岛";

    public int Hours { get; set; } = 24;

    public DateTime ForecastStartUtc { get; set; }

    public bool IsSearching { get; private set; }

    public bool IsLoadingAnalysis { get; private set; }

    public string? SearchError { get; private set; }

    public string? AnalysisError { get; private set; }

    public IReadOnlyList<DashboardLocationOption> LocationResults { get; private set; } =
        Array.Empty<DashboardLocationOption>();

    public DashboardLocationOption? SelectedLocation { get; private set; }

    public DashboardAnalysisResult? Result { get; private set; }

    public string SelectedTrendKey { get; private set; } = DashboardTrendKeys.Score;

    public DateTimeOffset? SelectedForecastTimeUtc { get; private set; }

    public DashboardHourlyDetail? SelectedHourlyDetail
    {
        get
        {
            if (Result is null || Result.HourlyDetails.Count == 0)
            {
                return null;
            }

            var selectedTime = SelectedForecastTimeUtc ?? Result.HourlyDetails[0].ForecastTimeUtc;
            return Result.HourlyDetails.FirstOrDefault(detail => detail.ForecastTimeUtc == selectedTime);
        }
    }

    public bool CanSubmit => SelectedLocation is not null && !IsLoadingAnalysis;

    public void SelectTrend(string trendKey)
    {
        if (Result?.TrendTabs.Any(tab => tab.Key == trendKey) != true)
        {
            return;
        }

        SelectedTrendKey = trendKey;
    }

    public void SelectHour(DateTimeOffset forecastTimeUtc)
    {
        if (Result?.HourlyDetails.Any(detail => detail.ForecastTimeUtc == forecastTimeUtc) != true)
        {
            return;
        }

        SelectedForecastTimeUtc = forecastTimeUtc;
    }

    public void SelectLocation(Guid locationId)
    {
        SelectedLocation = LocationResults.FirstOrDefault(location => location.Id == locationId);
        AnalysisError = null;
    }

    public async Task SearchLocationsAsync(CancellationToken cancellationToken = default)
    {
        IsSearching = true;
        SearchError = null;

        try
        {
            var locations = await _locationQueryService.SearchPresetsAsync(
                SearchText,
                cancellationToken: cancellationToken);
            LocationResults = locations.Select(ToLocationOption).ToArray();
            if (SelectedLocation is not null &&
                LocationResults.All(location => location.Id != SelectedLocation.Id))
            {
                SelectedLocation = null;
            }

            if (LocationResults.Count == 0)
            {
                SearchError = "没有找到匹配的预置地点。";
            }
        }
        catch (ArgumentException exception)
        {
            LocationResults = Array.Empty<DashboardLocationOption>();
            SelectedLocation = null;
            SearchError = exception.Message;
        }
        finally
        {
            IsSearching = false;
        }
    }

    public async Task SubmitAnalysisAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedLocation is null)
        {
            AnalysisError = "请先从候选列表中选择一个地点。";
            return;
        }

        CancelActiveRequest();
        _activeRequest = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requestVersion = Interlocked.Increment(ref _requestVersion);
        IsLoadingAnalysis = true;
        AnalysisError = null;

        try
        {
            var location = await _locationQueryService.GetByIdAsync(
                SelectedLocation.Id,
                _activeRequest.Token);
            if (location is null)
            {
                AnalysisError = "所选地点已不存在，请重新搜索。";
                Result = null;
                return;
            }

            var query = new MarineAnalysisQuery(
                location.Coordinates,
                new ForecastRange(GetForecastStartOffset(), Hours),
                location);
            var result = await _analysisQueryService.ExecuteAsync(query, _activeRequest.Token);

            if (requestVersion == _requestVersion)
            {
                Result = Project(result);
                SelectedTrendKey = DashboardTrendKeys.Score;
                SelectedForecastTimeUtc = Result.HourlyDetails.Count == 0
                    ? null
                    : Result.HourlyDetails[0].ForecastTimeUtc;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
        }
        catch (ProviderException)
        {
            Result = null;
            AnalysisError = "天气数据源暂时不可用，且当前没有可用缓存。请稍后重试。";
        }
        catch (ArgumentException exception)
        {
            Result = null;
            AnalysisError = exception.Message;
        }
        finally
        {
            if (requestVersion == _requestVersion)
            {
                IsLoadingAnalysis = false;
            }
        }
    }

    public void CancelActiveRequest()
    {
        if (_activeRequest is null)
        {
            return;
        }

        _activeRequest.Cancel();
        _activeRequest.Dispose();
        _activeRequest = null;
    }

    public void Dispose() => CancelActiveRequest();

    private DateTimeOffset GetForecastStartOffset()
    {
        var startUtc = DateTime.SpecifyKind(ForecastStartUtc, DateTimeKind.Utc);
        return new DateTimeOffset(startUtc, TimeSpan.Zero);
    }

    private static DashboardLocationOption ToLocationOption(Location location) => new(
        location.Id,
        location.DisplayName,
        ToApiName(location.LocationType),
        location.Latitude,
        location.Longitude,
        location.TimeZoneId,
        location.IsPreset ? "preset" : "catalog");

    private static DashboardAnalysisResult Project(MarineAnalysisQueryResult result)
    {
        var selectedPoint = result.Snapshot.Points
            .OrderBy(point => point.ForecastTimeUtc)
            .FirstOrDefault();
        var selectedAssessment = result.HourlyAssessments
            .OrderBy(assessment => assessment.ForecastTimeUtc)
            .FirstOrDefault();

        var metricCards = selectedPoint is null
            ? Array.Empty<DashboardMetricCard>()
            : CreateMetricCards(selectedPoint).ToArray();

        var cacheStatusByBatchId = new Dictionary<Guid, string>
        {
            [result.Weather.Batch.BatchId] = ToCacheStatus(result.Weather.Kind),
            [result.Marine.Batch.BatchId] = ToCacheStatus(result.Marine.Kind)
        };

        var sources = result.Snapshot.SourceBatches
            .OrderBy(source => source.DataDomain)
            .Select(source => new DashboardSourceStatus(
                ToApiName(source.DataDomain),
                source.Provider.ProviderCode,
                source.Provider.SourceModel,
                source.IssuedAtUtc,
                source.FetchedAtUtc,
                cacheStatusByBatchId.GetValueOrDefault(source.BatchId, "miss"),
                ToApiName(source.Quality.Status),
                ToApiName(source.Quality.Freshness)))
            .ToArray();

        var hourlyRows = result.Snapshot.Points
            .OrderBy(point => point.ForecastTimeUtc)
            .Select(point =>
            {
                var assessment = result.HourlyAssessments.FirstOrDefault(item =>
                    item.ForecastTimeUtc == point.ForecastTimeUtc);

                return new DashboardHourlyRow(
                    point.ForecastTimeUtc,
                    FormatMetric(point.Metrics.WindSpeedMs, "0.0"),
                    FormatMetric(point.Metrics.WindGustMs, "0.0"),
                    FormatMetric(point.Metrics.WaveHeightM, "0.0"),
                    FormatMetric(point.Metrics.SwellHeightM, "0.0"),
                    FormatVisibilityKm(point.Metrics.VisibilityM),
                    assessment is null ? "暂无" : FormatScore(assessment.Score),
                    assessment is null ? "unknown" : ToApiName(assessment.RiskLevel),
                    ToApiName(point.Quality.Status));
            })
            .ToArray();
        var hourlyDetails = result.Snapshot.Points
            .OrderBy(point => point.ForecastTimeUtc)
            .Select(point =>
            {
                var assessment = result.HourlyAssessments.FirstOrDefault(item =>
                    item.ForecastTimeUtc == point.ForecastTimeUtc);

                return new DashboardHourlyDetail(
                    point.ForecastTimeUtc,
                    ToApiName(point.Quality.Status),
                    ToApiName(point.Quality.Freshness),
                    FormatScore(assessment?.Score),
                    assessment is null ? "unknown" : ToApiName(assessment.RiskLevel),
                    assessment is null ? "数据不足" : ToRiskLevelText(assessment.RiskLevel),
                    CreateDetailMetrics(point).ToArray(),
                    assessment is null ? [] : ToActivityScores(assessment),
                    assessment is null ? [] : ToRiskSummaries(assessment),
                    point.MetricSources
                        .OrderBy(source => source.Metric)
                        .Select(source => new DashboardMetricSourceSummary(
                            ToApiName(source.Metric),
                            source.Provider.ProviderCode,
                            source.Provider.SourceModel,
                            source.ForecastTimeUtc,
                            ToApiName(source.QualityStatus),
                            ToApiName(source.Freshness)))
                        .ToArray());
            })
            .ToArray();
        var timelineWindows = ToTimelineWindows(result);

        return new DashboardAnalysisResult(
            result.Snapshot.SnapshotId,
            result.Query.LocationMetadata?.DisplayName ?? "自定义坐标",
            result.Query.Location.Latitude,
            result.Query.Location.Longitude,
            result.Query.LocationMetadata?.TimeZoneId,
            result.Snapshot.Range.StartUtc,
            result.Snapshot.Range.EndUtc,
            result.Snapshot.Range.Hours,
            ToApiName(result.Snapshot.Quality.Status),
            ToApiName(result.Snapshot.Quality.Freshness),
            result.Snapshot.Quality.Completeness,
            ToFlags(result.Snapshot.Quality.Flags),
            result.Snapshot.Quality.MissingMetrics.Select(ToApiName).ToArray(),
            result.Snapshot.Quality.MissingDomains.Select(ToApiName).ToArray(),
            selectedAssessment is null ? null : ToDashboardOverall(selectedAssessment),
            selectedAssessment is null ? [] : ToActivityScores(selectedAssessment),
            ToRecommendationWindows(result),
            selectedAssessment is null ? [] : ToRiskSummaries(selectedAssessment),
            sources,
            metricCards,
            CreateTrendTabs(result, timelineWindows).ToArray(),
            timelineWindows,
            hourlyDetails,
            hourlyRows,
            "结果仅供辅助决策，请以官方预警和现场管理为准。");
    }

    private static IEnumerable<DashboardMetricCard> CreateMetricCards(ForecastSnapshotPoint point)
    {
        var metrics = point.Metrics;

        yield return CreateMetric("风速", metrics.WindSpeedMs, "m/s", "平均风", point.Quality, ForecastMetricName.WindSpeedMs);
        yield return CreateMetric("阵风", metrics.WindGustMs, "m/s", "突增风", point.Quality, ForecastMetricName.WindGustMs);
        yield return CreateMetric("有效波高", metrics.WaveHeightM, "m", "海浪", point.Quality, ForecastMetricName.WaveHeightM);
        yield return CreateMetric("浪周期", metrics.WavePeriodS, "s", "波浪间隔", point.Quality, ForecastMetricName.WavePeriodS);
        yield return CreateMetric("涌浪", metrics.SwellHeightM, "m", FormatSwellDetail(metrics.SwellPeriodS), point.Quality, ForecastMetricName.SwellHeightM);
        yield return CreateMetric("能见度", metrics.VisibilityM is null ? null : metrics.VisibilityM / 1000, "km", "水平能见度", point.Quality, ForecastMetricName.VisibilityM);
        yield return new DashboardMetricCard(
            "雷暴",
            metrics.Thunderstorm is null ? "暂无数据" : metrics.Thunderstorm.Value ? "是" : "否",
            string.Empty,
            "对流信号",
            metrics.Thunderstorm.HasValue ? "已评分" : "暂无数据",
            ToApiName(point.Quality.Status));
    }

    private static DashboardMetricCard CreateMetric(
        string label,
        double? value,
        string unit,
        string detail,
        SnapshotQuality quality,
        ForecastMetricName metricName)
    {
        var hasMissingFlag = quality.MissingMetrics.Contains(metricName);
        return new DashboardMetricCard(
            label,
            FormatMetric(value, "0.0"),
            value.HasValue ? unit : string.Empty,
            detail,
            value.HasValue && !hasMissingFlag ? "已评分" : "暂无数据",
            ToApiName(quality.Status));
    }

    private static DashboardOverallAssessment ToDashboardOverall(
        HourlyMarineAssessment assessment) => new(
            FormatScore(assessment.Score),
            ToApiName(assessment.RiskLevel),
            ToRiskLevelText(assessment.RiskLevel),
            assessment.Confidence.ToString("P0", CultureInfo.InvariantCulture),
            assessment.AlgorithmVersion);

    private static DashboardActivityScore[] ToActivityScores(
        HourlyMarineAssessment assessment) =>
        assessment.ActivityAssessments
            .Select(activity => new DashboardActivityScore(
                ToActivityLabel(activity.ActivityType),
                ToApiName(activity.ActivityType),
                FormatScore(activity.Score),
                ToApiName(activity.RiskLevel),
                ToRiskLevelText(activity.RiskLevel)))
            .ToArray();

    private static DashboardRecommendationWindow[] ToRecommendationWindows(
        MarineAnalysisQueryResult result) =>
        result.RecommendedWindows
            .Select(window => new DashboardRecommendationWindow(
                ToActivityLabel(window.ActivityType),
                ToApiName(window.ActivityType),
                window.StartUtc,
                window.EndUtc,
                FormatScore(window.BestScore),
                FormatScore(window.MinimumScore),
                window.DurationHours,
                window.ReturnBeforeUtc,
                window.RiskRisesAtUtc,
                window.RiskReason))
            .ToArray();

    private static DashboardRiskSummary[] ToRiskSummaries(
        HourlyMarineAssessment assessment) =>
        assessment.Contributions
            .Where(contribution => contribution.Penalty > 0)
            .OrderByDescending(contribution => contribution.Penalty)
            .Take(5)
            .Select(contribution => new DashboardRiskSummary(
                contribution.Code,
                ToApiName(contribution.Severity),
                FormatMetric(contribution.Penalty, "0.#"),
                contribution.Message))
            .ToArray();

    private static IEnumerable<DashboardTrendTab> CreateTrendTabs(
        MarineAnalysisQueryResult result,
        IReadOnlyList<DashboardTimelineWindow> timelineWindows)
    {
        var orderedPoints = result.Snapshot.Points
            .OrderBy(point => point.ForecastTimeUtc)
            .ToArray();
        var assessmentByTime = result.HourlyAssessments.ToDictionary(assessment => assessment.ForecastTimeUtc);
        var maxWind = MaxAtLeastOne(orderedPoints.SelectMany(point =>
            new[] { point.Metrics.WindSpeedMs, point.Metrics.WindGustMs }));
        var maxWave = MaxAtLeastOne(orderedPoints.SelectMany(point =>
            new[] { point.Metrics.WaveHeightM, point.Metrics.SwellHeightM }));

        yield return new DashboardTrendTab(
            DashboardTrendKeys.Score,
            "分数",
            "综合分",
            "风险等级",
            orderedPoints.Select(point =>
            {
                assessmentByTime.TryGetValue(point.ForecastTimeUtc, out var assessment);
                return CreateTrendPoint(
                    point.ForecastTimeUtc,
                    assessment?.Score,
                    null,
                    100,
                    FormatScore(assessment?.Score),
                    assessment is null ? "暂无" : ToRiskLevelText(assessment.RiskLevel),
                    assessment is null ? "unknown" : ToApiName(assessment.RiskLevel),
                    point.Quality,
                    timelineWindows);
            }).ToArray());

        yield return new DashboardTrendTab(
            DashboardTrendKeys.Wind,
            "风",
            "风速",
            "阵风",
            orderedPoints.Select(point => CreateTrendPoint(
                point.ForecastTimeUtc,
                point.Metrics.WindSpeedMs,
                point.Metrics.WindGustMs,
                maxWind,
                FormatMetric(point.Metrics.WindSpeedMs, "0.0"),
                FormatMetric(point.Metrics.WindGustMs, "0.0"),
                assessmentByTime.TryGetValue(point.ForecastTimeUtc, out var assessment)
                    ? ToApiName(assessment.RiskLevel)
                    : "unknown",
                point.Quality,
                timelineWindows)).ToArray());

        yield return new DashboardTrendTab(
            DashboardTrendKeys.Wave,
            "浪",
            "浪高",
            "涌浪",
            orderedPoints.Select(point => CreateTrendPoint(
                point.ForecastTimeUtc,
                point.Metrics.WaveHeightM,
                point.Metrics.SwellHeightM,
                maxWave,
                FormatMetric(point.Metrics.WaveHeightM, "0.0"),
                FormatMetric(point.Metrics.SwellHeightM, "0.0"),
                assessmentByTime.TryGetValue(point.ForecastTimeUtc, out var assessment)
                    ? ToApiName(assessment.RiskLevel)
                    : "unknown",
                point.Quality,
                timelineWindows)).ToArray());
    }

    private static DashboardTrendPoint CreateTrendPoint(
        DateTimeOffset forecastTimeUtc,
        double? primary,
        double? secondary,
        double scale,
        string primaryValue,
        string secondaryValue,
        string riskLevel,
        SnapshotQuality quality,
        IReadOnlyList<DashboardTimelineWindow> timelineWindows) => new(
        forecastTimeUtc,
        forecastTimeUtc.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture),
        primaryValue,
        secondaryValue,
        riskLevel,
        ToApiName(quality.Status),
        ToPercent(primary, scale),
        ToPercent(secondary, scale),
        timelineWindows.Any(window => forecastTimeUtc >= window.StartUtc && forecastTimeUtc < window.EndUtc),
        timelineWindows.Any(window => window.RiskRisesAtUtc == forecastTimeUtc),
        timelineWindows.Any(window => window.ReturnBeforeUtc.HasValue &&
            Math.Abs((window.ReturnBeforeUtc.Value - forecastTimeUtc).TotalMinutes) <= 30));

    private static DashboardTimelineWindow[] ToTimelineWindows(MarineAnalysisQueryResult result)
    {
        var rangeStart = result.Snapshot.Range.StartUtc;
        var rangeMinutes = Math.Max((result.Snapshot.Range.EndUtc - rangeStart).TotalMinutes, 1);

        return result.RecommendedWindows
            .Select(window =>
            {
                var startPercent = Math.Clamp((window.StartUtc - rangeStart).TotalMinutes / rangeMinutes * 100, 0, 100);
                var endPercent = Math.Clamp((window.EndUtc - rangeStart).TotalMinutes / rangeMinutes * 100, 0, 100);

                return new DashboardTimelineWindow(
                    ToActivityLabel(window.ActivityType),
                    ToApiName(window.ActivityType),
                    window.StartUtc,
                    window.EndUtc,
                    window.ReturnBeforeUtc,
                    window.RiskRisesAtUtc,
                    window.RiskReason,
                    startPercent.ToString("0.##", CultureInfo.InvariantCulture),
                    Math.Max(endPercent - startPercent, 0.5).ToString("0.##", CultureInfo.InvariantCulture));
            })
            .ToArray();
    }

    private static IEnumerable<DashboardDetailMetric> CreateDetailMetrics(ForecastSnapshotPoint point)
    {
        var metrics = point.Metrics;

        yield return new DashboardDetailMetric("风速", FormatMetric(metrics.WindSpeedMs, "0.0"), "m/s");
        yield return new DashboardDetailMetric("阵风", FormatMetric(metrics.WindGustMs, "0.0"), "m/s");
        yield return new DashboardDetailMetric("风向", FormatMetric(metrics.WindDirectionDeg, "0"), "deg");
        yield return new DashboardDetailMetric("有效波高", FormatMetric(metrics.WaveHeightM, "0.0"), "m");
        yield return new DashboardDetailMetric("浪周期", FormatMetric(metrics.WavePeriodS, "0.0"), "s");
        yield return new DashboardDetailMetric("涌浪", FormatMetric(metrics.SwellHeightM, "0.0"), "m");
        yield return new DashboardDetailMetric("涌浪周期", FormatMetric(metrics.SwellPeriodS, "0.0"), "s");
        yield return new DashboardDetailMetric("能见度", FormatVisibilityKm(metrics.VisibilityM), "km");
        yield return new DashboardDetailMetric("降水", FormatMetric(metrics.PrecipitationMmPerHour, "0.0"), "mm/h");
        yield return new DashboardDetailMetric("CAPE", FormatMetric(metrics.CapeJkg, "0"), "J/kg");
        yield return new DashboardDetailMetric("雷暴", metrics.Thunderstorm is null ? "暂无数据" : metrics.Thunderstorm.Value ? "是" : "否", string.Empty);
        yield return new DashboardDetailMetric("气温", FormatMetric(metrics.TemperatureC, "0.0"), "C");
    }

    private static int ToPercent(double? value, double scale) =>
        value.HasValue
            ? (int)Math.Clamp(Math.Round(value.Value / Math.Max(scale, 1) * 100), 0, 100)
            : 0;

    private static double MaxAtLeastOne(IEnumerable<double?> values) =>
        Math.Max(values.Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty(1).Max(), 1);

    private static string FormatScore(double? score) =>
        score.HasValue
            ? score.Value.ToString("0", CultureInfo.InvariantCulture)
            : "暂无";

    private static string ToActivityLabel(ActivityType activityType) => activityType switch
    {
        ActivityType.ShoreFishing => "岸钓",
        ActivityType.Boat => "乘船",
        ActivityType.Landing => "登岛",
        ActivityType.Camping => "露营",
        ActivityType.Photography => "摄影",
        _ => activityType.ToString()
    };

    private static string ToRiskLevelText(RiskLevel riskLevel) => riskLevel switch
    {
        RiskLevel.VeryGood => "非常适宜",
        RiskLevel.Good => "适宜",
        RiskLevel.Moderate => "一般",
        RiskLevel.Caution => "谨慎",
        RiskLevel.Avoid => "不建议",
        RiskLevel.Unknown => "数据不足",
        _ => riskLevel.ToString()
    };

    private static string FormatSwellDetail(double? swellPeriodS) =>
        swellPeriodS.HasValue
            ? $"周期 {swellPeriodS.Value:0.0} s"
            : "涌浪周期";

    private static string FormatMetric(double? value, string format) =>
        value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) : "暂无数据";

    private static string FormatVisibilityKm(double? visibilityM) =>
        visibilityM.HasValue
            ? (visibilityM.Value / 1000).ToString("0.0", CultureInfo.InvariantCulture)
            : "暂无数据";

    private static string[] ToFlags(ForecastQualityMask flags) =>
        Enum.GetValues<ForecastQualityMask>()
            .Where(flag => flag != ForecastQualityMask.None && flags.HasFlag(flag))
            .Select(ToApiName)
            .ToArray();

    private static string ToCacheStatus(ForecastCacheResultKind kind) => kind switch
    {
        ForecastCacheResultKind.FreshCache => "hit",
        ForecastCacheResultKind.StaleCache => "stale",
        _ => "miss"
    };

    private static string ToApiName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        return name.Length == 0
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static DateTimeOffset RoundToNextUtcHour(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return new DateTimeOffset(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            0,
            0,
            TimeSpan.Zero).AddHours(1);
    }
}

public sealed record DashboardLocationOption(
    Guid Id,
    string DisplayName,
    string LocationType,
    double Latitude,
    double Longitude,
    string TimeZone,
    string Source);

public sealed record DashboardAnalysisResult(
    Guid SnapshotId,
    string DisplayName,
    double Latitude,
    double Longitude,
    string? TimeZone,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int Hours,
    string QualityStatus,
    string Freshness,
    double Completeness,
    IReadOnlyList<string> Flags,
    IReadOnlyList<string> MissingMetrics,
    IReadOnlyList<string> MissingDomains,
    DashboardOverallAssessment? Overall,
    IReadOnlyList<DashboardActivityScore> ActivityScores,
    IReadOnlyList<DashboardRecommendationWindow> RecommendationWindows,
    IReadOnlyList<DashboardRiskSummary> TopRisks,
    IReadOnlyList<DashboardSourceStatus> Sources,
    IReadOnlyList<DashboardMetricCard> MetricCards,
    IReadOnlyList<DashboardTrendTab> TrendTabs,
    IReadOnlyList<DashboardTimelineWindow> TimelineWindows,
    IReadOnlyList<DashboardHourlyDetail> HourlyDetails,
    IReadOnlyList<DashboardHourlyRow> HourlyRows,
    string Disclaimer);

public sealed record DashboardSourceStatus(
    string DataDomain,
    string Provider,
    string Model,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset FetchedAtUtc,
    string CacheStatus,
    string QualityStatus,
    string Freshness);

public sealed record DashboardMetricCard(
    string Label,
    string Value,
    string Unit,
    string Detail,
    string StatusText,
    string QualityStatus);

public sealed record DashboardTrendTab(
    string Key,
    string Label,
    string PrimaryLabel,
    string SecondaryLabel,
    IReadOnlyList<DashboardTrendPoint> Points);

public sealed record DashboardTrendPoint(
    DateTimeOffset ForecastTimeUtc,
    string TimeLabel,
    string PrimaryValue,
    string SecondaryValue,
    string RiskLevel,
    string QualityStatus,
    int PrimaryPercent,
    int SecondaryPercent,
    bool IsRecommended,
    bool IsRiskRise,
    bool IsReturnBefore);

public sealed record DashboardTimelineWindow(
    string Label,
    string Activity,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    DateTimeOffset? ReturnBeforeUtc,
    DateTimeOffset? RiskRisesAtUtc,
    string? RiskReason,
    string StartPercent,
    string WidthPercent);

public sealed record DashboardOverallAssessment(
    string Score,
    string RiskLevel,
    string RiskLevelText,
    string Confidence,
    string AlgorithmVersion);

public sealed record DashboardActivityScore(
    string Label,
    string Type,
    string Score,
    string RiskLevel,
    string RiskLevelText);

public sealed record DashboardRecommendationWindow(
    string Label,
    string Activity,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string BestScore,
    string MinimumScore,
    int DurationHours,
    DateTimeOffset? ReturnBeforeUtc,
    DateTimeOffset? RiskRisesAtUtc,
    string? RiskReason);

public sealed record DashboardRiskSummary(
    string Code,
    string Severity,
    string Penalty,
    string Message);

public sealed record DashboardHourlyDetail(
    DateTimeOffset ForecastTimeUtc,
    string QualityStatus,
    string Freshness,
    string Score,
    string RiskLevel,
    string RiskLevelText,
    IReadOnlyList<DashboardDetailMetric> Metrics,
    IReadOnlyList<DashboardActivityScore> ActivityScores,
    IReadOnlyList<DashboardRiskSummary> Risks,
    IReadOnlyList<DashboardMetricSourceSummary> Sources);

public sealed record DashboardDetailMetric(
    string Label,
    string Value,
    string Unit);

public sealed record DashboardMetricSourceSummary(
    string Metric,
    string Provider,
    string Model,
    DateTimeOffset ForecastTimeUtc,
    string QualityStatus,
    string Freshness);

public sealed record DashboardHourlyRow(
    DateTimeOffset ForecastTimeUtc,
    string WindSpeedMs,
    string WindGustMs,
    string WaveHeightM,
    string SwellHeightM,
    string VisibilityKm,
    string Score,
    string RiskLevel,
    string QualityStatus);

public static class DashboardTrendKeys
{
    public const string Score = "score";
    public const string Wind = "wind";
    public const string Wave = "wave";
}
