namespace MarineInsight.Web.Api;

public sealed record MarineAnalysisRequest
{
    public MarineAnalysisLocationInput? Location { get; init; }

    public DateTimeOffset From { get; init; }

    public int Hours { get; init; }

    public IReadOnlyList<string>? Activities { get; init; }

    public MarineAnalysisUnitsInput? Units { get; init; }
}

public sealed record MarineAnalysisLocationInput
{
    public Guid? LocationId { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}

public sealed record MarineAnalysisUnitsInput
{
    public string? WindSpeed { get; init; }

    public string? WaveHeight { get; init; }

    public string? Temperature { get; init; }
}

public sealed record MarineAnalysisResponse(
    string AnalysisStatus,
    Guid AnalysisId,
    MarineAnalysisLocationResponse Location,
    MarineAnalysisRangeResponse Range,
    IReadOnlyList<MarineAnalysisSourceResponse> Sources,
    MarineAnalysisQualityResponse Quality,
    MarineAnalysisOverallResponse? Overall,
    IReadOnlyList<MarineAnalysisActivityResponse> Activities,
    IReadOnlyList<MarineAnalysisRiskResponse> Risks,
    IReadOnlyList<MarineAnalysisHourlyResponse> Hourly,
    string Disclaimer,
    string TraceId);

public sealed record MarineAnalysisLocationResponse(
    double Latitude,
    double Longitude,
    Guid? LocationId = null,
    string? DisplayName = null,
    string? TimeZone = null);

public sealed record MarineAnalysisRangeResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int Hours);

public sealed record MarineAnalysisSourceResponse(
    Guid BatchId,
    string DataDomain,
    string Provider,
    string Model,
    DateTimeOffset IssuedAt,
    DateTimeOffset FetchedAt,
    string CacheStatus,
    MarineAnalysisQualityResponse Quality);

public sealed record MarineAnalysisHourlyResponse(
    DateTimeOffset ForecastTime,
    MarineAnalysisMetricsResponse Metrics,
    MarineAnalysisQualityResponse Quality,
    IReadOnlyList<MarineAnalysisMetricSourceResponse> Sources,
    MarineAnalysisOverallResponse? Overall,
    IReadOnlyList<MarineAnalysisActivityResponse> Activities,
    IReadOnlyList<MarineAnalysisRiskResponse> Risks);

public sealed record MarineAnalysisOverallResponse(
    double? Score,
    string RiskLevel,
    double Confidence,
    string AlgorithmVersion);

public sealed record MarineAnalysisActivityResponse(
    string Type,
    double? Score,
    string RiskLevel,
    double Confidence);

public sealed record MarineAnalysisRiskResponse(
    string Code,
    string Kind,
    string Severity,
    DateTimeOffset ForecastTime,
    string Metric,
    double? Actual,
    double? Threshold,
    double Penalty,
    string Message);

public sealed record MarineAnalysisMetricSourceResponse(
    string Metric,
    Guid BatchId,
    string Provider,
    string Model,
    DateTimeOffset ForecastTime,
    string QualityStatus,
    string Freshness);

public sealed record MarineAnalysisQualityResponse(
    string Status,
    string Freshness,
    double Completeness,
    IReadOnlyList<string> Flags,
    IReadOnlyList<string> MissingMetrics,
    IReadOnlyList<string> MissingDomains);

public sealed record MarineAnalysisMetricsResponse(
    double? WindSpeedMs,
    double? WindGustMs,
    double? WindDirectionDeg,
    double? TemperatureC,
    double? RelativeHumidityPct,
    double? SurfacePressureHpa,
    double? CloudCoverPct,
    double? PrecipitationMmPerHour,
    double? CapeJkg,
    double? VisibilityM,
    int? WeatherCode,
    bool? Thunderstorm,
    double? WaveHeightM,
    double? WavePeriodS,
    double? WavePeakPeriodS,
    double? WaveDirectionDeg,
    double? WindWaveHeightM,
    double? WindWavePeriodS,
    double? WindWavePeakPeriodS,
    double? WindWaveDirectionDeg,
    double? SwellHeightM,
    double? SwellPeriodS,
    double? SwellPeakPeriodS,
    double? SwellDirectionDeg,
    double? SeaTemperatureC,
    double? CurrentSpeedMs,
    double? CurrentDirectionDeg,
    double? TideHeightM,
    string? TideType);
