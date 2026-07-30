using System.Globalization;
using MarineInsight.Application.Analysis;
using MarineInsight.Application.Errors;
using MarineInsight.Application.Forecast;
using MarineInsight.Application.Locations;
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

    public bool CanSubmit => SelectedLocation is not null && !IsLoadingAnalysis;

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
            .Select(point => new DashboardHourlyRow(
                point.ForecastTimeUtc,
                FormatMetric(point.Metrics.WindSpeedMs, "0.0"),
                FormatMetric(point.Metrics.WindGustMs, "0.0"),
                FormatMetric(point.Metrics.WaveHeightM, "0.0"),
                FormatMetric(point.Metrics.SwellHeightM, "0.0"),
                FormatVisibilityKm(point.Metrics.VisibilityM),
                ToApiName(point.Quality.Status)))
            .ToArray();

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
            sources,
            metricCards,
            hourlyRows,
            "结果仅供辅助决策，请以官方预警和现场管理为准。");
    }

    private static IEnumerable<DashboardMetricCard> CreateMetricCards(ForecastSnapshotPoint point)
    {
        var metrics = point.Metrics;

        // metrics-only 阶段只展示原始指标可用性，不能在确定性评分规则落地前暗示安全结论。
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
            metrics.Thunderstorm.HasValue ? "待评分" : "暂无数据",
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
            value.HasValue && !hasMissingFlag ? "待评分" : "暂无数据",
            ToApiName(quality.Status));
    }

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
    IReadOnlyList<DashboardSourceStatus> Sources,
    IReadOnlyList<DashboardMetricCard> MetricCards,
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

public sealed record DashboardHourlyRow(
    DateTimeOffset ForecastTimeUtc,
    string WindSpeedMs,
    string WindGustMs,
    string WaveHeightM,
    string SwellHeightM,
    string VisibilityKm,
    string QualityStatus);
