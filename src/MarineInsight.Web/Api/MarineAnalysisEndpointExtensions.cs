using System.Diagnostics;
using MarineInsight.Application.Analysis;
using MarineInsight.Application.Errors;
using MarineInsight.Application.Forecast;
using MarineInsight.Domain.Forecast;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MarineInsight.Web.Api;

public static class MarineAnalysisEndpointExtensions
{
    public static IEndpointRouteBuilder MapMarineAnalysisEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGroup("/api/v1")
            .MapPost("/marine-analyses", HandleAsync)
            .AllowAnonymous()
            .WithName("CreateMarineAnalysis")
            .Produces<MarineAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        MarineAnalysisRequest? request,
        MarineAnalysisQueryService queryService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var traceId = GetTraceId(httpContext);
        httpContext.Response.Headers["Trace-Id"] = traceId;

        if (!TryCreateQuery(request, out var query, out var validationErrors))
        {
            return Results.ValidationProblem(
                validationErrors,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Request validation failed.",
                type: "https://marine-insight.local/problems/validation-failed",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = MarineInsightErrorCodes.ValidationFailed,
                    ["traceId"] = traceId
                });
        }

        try
        {
            var result = await queryService.ExecuteAsync(query!, cancellationToken);
            return Results.Ok(Project(result, traceId));
        }
        catch (ProviderException exception)
        {
            return CreateProviderProblem(exception, traceId);
        }
    }

    private static bool TryCreateQuery(
        MarineAnalysisRequest? request,
        out MarineAnalysisQuery? query,
        out Dictionary<string, string[]> errors)
    {
        query = null;
        errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request is null)
        {
            errors["request"] = ["The request body is required."];
            return false;
        }

        if (request.Location is null)
        {
            errors["location"] = ["A coordinate location is required for the metrics-only query."];
        }
        else
        {
            var hasLatitude = request.Location.Latitude.HasValue;
            var hasLongitude = request.Location.Longitude.HasValue;
            if (hasLatitude != hasLongitude)
            {
                errors["location"] = ["Latitude and longitude must be provided together."];
            }
            else if (!hasLatitude)
            {
                errors["location"] = [
                    request.Location.LocationId.HasValue
                        ? "LocationId lookup is not available in the metrics-only skeleton; provide coordinates."
                        : "Latitude and longitude are required."
                ];
            }
            else
            {
                try
                {
                    _ = new GeoPoint(
                        request.Location.Latitude!.Value,
                        request.Location.Longitude!.Value);
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    errors["location"] = [exception.Message];
                }

                if (request.Location.LocationId.HasValue)
                {
                    errors["location"] = ["Use either locationId or coordinates, not both."];
                }
            }
        }

        if (request.From == default)
        {
            errors["from"] = ["A UTC-offset forecast start time is required."];
        }

        if (request.Hours is not (24 or 72 or 168))
        {
            errors["hours"] = ["Hours must be 24, 72, or 168."];
        }

        if (errors.Count > 0)
        {
            return false;
        }

        try
        {
            query = new MarineAnalysisQuery(
                new GeoPoint(
                    request.Location!.Latitude!.Value,
                    request.Location.Longitude!.Value),
                new ForecastRange(request.From, request.Hours));
            return true;
        }
        catch (ArgumentException exception)
        {
            errors["request"] = [exception.Message];
            return false;
        }
    }

    private static MarineAnalysisResponse Project(
        MarineAnalysisQueryResult result,
        string traceId)
    {
        var cacheStatusByBatchId = new Dictionary<Guid, string>
        {
            [result.Weather.Batch.BatchId] = ToCacheStatus(result.Weather.Kind),
            [result.Marine.Batch.BatchId] = ToCacheStatus(result.Marine.Kind)
        };

        var sourceBatches = result.Snapshot.SourceBatches
            .Select(source => new MarineAnalysisSourceResponse(
                source.BatchId,
                ToApiName(source.DataDomain),
                source.Provider.ProviderCode,
                source.Provider.SourceModel,
                source.IssuedAtUtc,
                source.FetchedAtUtc,
                cacheStatusByBatchId.GetValueOrDefault(source.BatchId, "miss"),
                ToQuality(source.Quality)))
            .ToArray();

        var hourly = result.Snapshot.Points
            .Select(point => new MarineAnalysisHourlyResponse(
                point.ForecastTimeUtc,
                ToMetrics(point.Metrics),
                ToQuality(point.Quality),
                point.MetricSources
                    .OrderBy(source => source.Metric)
                    .Select(source => new MarineAnalysisMetricSourceResponse(
                        ToApiName(source.Metric),
                        source.BatchId,
                        source.Provider.ProviderCode,
                        source.Provider.SourceModel,
                        source.ForecastTimeUtc,
                        ToApiName(source.QualityStatus),
                        ToApiName(source.Freshness)))
                    .ToArray()))
            .ToArray();

        return new MarineAnalysisResponse(
            "metricsOnly",
            result.Snapshot.SnapshotId,
            new MarineAnalysisLocationResponse(
                result.Snapshot.RequestedLocation.Latitude,
                result.Snapshot.RequestedLocation.Longitude),
            new MarineAnalysisRangeResponse(
                result.Snapshot.Range.StartUtc,
                result.Snapshot.Range.EndUtc,
                result.Snapshot.Range.Hours),
            sourceBatches,
            ToQuality(result.Snapshot.Quality),
            hourly,
            "结果仅供辅助决策，请以官方预警和现场管理为准。",
            traceId);
    }

    private static MarineAnalysisMetricsResponse ToMetrics(ForecastMetricSet metrics) => new(
        metrics.WindSpeedMs,
        metrics.WindGustMs,
        metrics.WindDirectionDeg,
        metrics.TemperatureC,
        metrics.RelativeHumidityPct,
        metrics.SurfacePressureHpa,
        metrics.CloudCoverPct,
        metrics.PrecipitationMmPerHour,
        metrics.CapeJkg,
        metrics.VisibilityM,
        metrics.WeatherCode,
        metrics.Thunderstorm,
        metrics.WaveHeightM,
        metrics.WavePeriodS,
        metrics.WavePeakPeriodS,
        metrics.WaveDirectionDeg,
        metrics.WindWaveHeightM,
        metrics.WindWavePeriodS,
        metrics.WindWavePeakPeriodS,
        metrics.WindWaveDirectionDeg,
        metrics.SwellHeightM,
        metrics.SwellPeriodS,
        metrics.SwellPeakPeriodS,
        metrics.SwellDirectionDeg,
        metrics.SeaTemperatureC,
        metrics.CurrentSpeedMs,
        metrics.CurrentDirectionDeg,
        metrics.TideHeightM,
        metrics.TideType is { } tideType ? ToApiName(tideType) : null);

    private static MarineAnalysisQualityResponse ToQuality(DataQuality quality) => new(
        ToApiName(quality.Status),
        ToApiName(quality.Freshness),
        quality.Completeness,
        ToFlags(quality.Flags),
        quality.MissingMetrics.Select(ToApiName).ToArray(),
        Array.Empty<string>());

    private static MarineAnalysisQualityResponse ToQuality(SnapshotQuality quality) => new(
        ToApiName(quality.Status),
        ToApiName(quality.Freshness),
        quality.Completeness,
        ToFlags(quality.Flags),
        quality.MissingMetrics.Select(ToApiName).ToArray(),
        quality.MissingDomains.Select(ToApiName).ToArray());

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

    private static string GetTraceId(HttpContext httpContext) =>
        Activity.Current?.Id ?? httpContext.TraceIdentifier;

    private static IResult CreateProviderProblem(
        ProviderException exception,
        string traceId)
    {
        var statusCode = exception.FailureKind == ProviderFailureKind.RateLimited
            ? StatusCodes.Status429TooManyRequests
            : StatusCodes.Status503ServiceUnavailable;
        var code = exception.FailureKind == ProviderFailureKind.RateLimited
            ? MarineInsightErrorCodes.RateLimited
            : MarineInsightErrorCodes.ProviderUnavailable;

        return Results.Problem(
            statusCode: statusCode,
            title: statusCode == StatusCodes.Status429TooManyRequests
                ? "Forecast provider rate limit reached."
                : "Forecast provider is temporarily unavailable.",
            detail: "No live forecast or usable stale cache is available.",
            type: $"https://marine-insight.local/problems/{code.ToLowerInvariant().Replace('_', '-')}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = traceId
            });
    }
}
