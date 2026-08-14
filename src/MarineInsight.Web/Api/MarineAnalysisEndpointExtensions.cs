using System.Diagnostics;
using MarineInsight.Application.Analysis;
using MarineInsight.Application.Errors;
using MarineInsight.Application.Forecast;
using MarineInsight.Application.Locations;
using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
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
            .Produces(StatusCodes.Status304NotModified)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting("analysis");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        MarineAnalysisRequest? request,
        MarineAnalysisQueryService queryService,
        ExplanationService explanationService,
        LocationQueryService locationQueryService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var traceId = GetTraceId(httpContext);
        httpContext.Response.Headers["Trace-Id"] = traceId;

        var queryCreation = await CreateQueryAsync(
            request,
            locationQueryService,
            cancellationToken);
        if (queryCreation.MissingLocationId is { } missingLocationId)
        {
            return CreateLocationNotFoundProblem(missingLocationId, traceId);
        }

        if (queryCreation.Query is null)
        {
            return Results.ValidationProblem(
                queryCreation.Errors,
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
            var result = await queryService.ExecuteAsync(queryCreation.Query, cancellationToken);
            httpContext.Response.Headers.ETag = result.CacheIdentity.ETag;
            if (IsNotModified(httpContext, result.CacheIdentity.ETag))
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            var explanation = await explanationService.GenerateAsync(result, cancellationToken);
            return Results.Ok(Project(result, explanation, traceId));
        }
        catch (ProviderException exception)
        {
            return CreateProviderProblem(exception, traceId);
        }
    }

    private static async Task<QueryCreationResult> CreateQueryAsync(
        MarineAnalysisRequest? request,
        LocationQueryService locationQueryService,
        CancellationToken cancellationToken)
    {
        if (request?.Location?.LocationId is null ||
            request.Location.Latitude.HasValue ||
            request.Location.Longitude.HasValue)
        {
            return TryCreateCoordinateQuery(request);
        }

        var locationId = request.Location.LocationId.Value;

        if (locationId == Guid.Empty)
        {
            return new QueryCreationResult(
                null,
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["location"] = ["LocationId must be a non-empty UUID."]
                },
                null);
        }

        var location = await locationQueryService.GetByIdAsync(locationId, cancellationToken);
        if (location is null)
        {
            return new QueryCreationResult(
                null,
                new Dictionary<string, string[]>(StringComparer.Ordinal),
                locationId);
        }

        // Resolve the catalog id once at the API boundary. Forecast providers continue
        // to receive only the normalized GeoPoint while the selected metadata is retained
        // for the response projection.
        var resolvedRequest = request with
        {
            Location = request.Location with
            {
                LocationId = null,
                Latitude = location.Latitude,
                Longitude = location.Longitude
            }
        };
        var coordinateResult = TryCreateCoordinateQuery(resolvedRequest);
        if (coordinateResult.Query is null)
        {
            return coordinateResult;
        }

        return coordinateResult with
        {
            Query = new MarineAnalysisQuery(
                coordinateResult.Query.Location,
                coordinateResult.Query.Range,
                location,
                coordinateResult.Query.Activities)
        };
    }

    private static QueryCreationResult TryCreateCoordinateQuery(MarineAnalysisRequest? request)
    {
        return TryCreateQuery(request, out var query, out var errors)
            ? new QueryCreationResult(query, errors, null)
            : new QueryCreationResult(null, errors, null);
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
                new ForecastRange(request.From, request.Hours),
                activities: ParseActivities(request));
            return true;
        }
        catch (ArgumentException exception)
        {
            errors["request"] = [exception.Message];
            return false;
        }
    }

    private static ActivityType[] ParseActivities(MarineAnalysisRequest request) =>
        request.Activities?
            .Select(ParseActivity)
            .ToArray() ?? [];

    private static ActivityType ParseActivity(string value) =>
        value switch
        {
            "shoreFishing" => ActivityType.ShoreFishing,
            "boat" => ActivityType.Boat,
            "landing" => ActivityType.Landing,
            "camping" => ActivityType.Camping,
            "photography" => ActivityType.Photography,
            _ => throw new ArgumentException($"Unsupported activity '{value}'.", nameof(value))
        };

    private sealed record QueryCreationResult(
        MarineAnalysisQuery? Query,
        Dictionary<string, string[]> Errors,
        Guid? MissingLocationId);

    private static MarineAnalysisResponse Project(
        MarineAnalysisQueryResult result,
        AnalysisExplanation explanation,
        string traceId)
    {
        var cacheStatusByBatchId = new Dictionary<Guid, string>
        {
            [result.Weather.Batch.BatchId] = ToCacheStatus(result.Weather.Kind),
            [result.Marine.Batch.BatchId] = ToCacheStatus(result.Marine.Kind)
        };
        if (result.Tide.Result is { } tideResult)
        {
            cacheStatusByBatchId[tideResult.Batch.BatchId] = result.Tide.CacheStatus;
        }

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

        var assessmentsByTime = result.HourlyAssessments.ToDictionary(
            assessment => assessment.ForecastTimeUtc);
        var rootAssessment = result.HourlyAssessments
            .OrderBy(assessment => assessment.ForecastTimeUtc)
            .FirstOrDefault();
        var rootRisks = result.HourlyAssessments
            .SelectMany(ProjectRisks)
            .OrderByDescending(risk => risk.Penalty)
            .ThenBy(risk => risk.ForecastTime)
            .Take(8)
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
                    .ToArray(),
                assessmentsByTime.TryGetValue(point.ForecastTimeUtc, out var assessment)
                    ? ProjectOverall(assessment)
                    : null,
                assessment is null
                    ? []
                    : ProjectActivities(assessment),
                assessment is null
                    ? []
                    : ProjectRisks(assessment)))
            .ToArray();

        return new MarineAnalysisResponse(
            "analyzed",
            result.Snapshot.SnapshotId,
            result.CacheIdentity.AlgorithmVersion,
            new MarineAnalysisCacheResponse(
                result.CacheIdentity.Value,
                result.CacheIdentity.ETag,
                result.CacheIdentity.SourceBatchSetHash,
                result.CacheIdentity.SourceSelectionPolicy,
                result.CacheIdentity.AlgorithmVersion,
                result.CacheIdentity.Activities.Select(ToApiName).ToArray()),
            new MarineAnalysisTideResponse(
                result.Tide.Status,
                result.Tide.CacheStatus,
                result.Tide.RemainingCredits,
                result.Tide.ErrorCode),
            new MarineAnalysisLocationResponse(
                result.Snapshot.RequestedLocation.Latitude,
                result.Snapshot.RequestedLocation.Longitude,
                result.Query.LocationMetadata?.Id,
                result.Query.LocationMetadata?.DisplayName,
                result.Query.LocationMetadata?.TimeZoneId),
            new MarineAnalysisRangeResponse(
                result.Snapshot.Range.StartUtc,
                result.Snapshot.Range.EndUtc,
                result.Snapshot.Range.Hours),
            sourceBatches,
            ToQuality(result.Snapshot.Quality),
            rootAssessment is null ? null : ProjectOverall(rootAssessment),
            rootAssessment is null ? [] : ProjectActivities(rootAssessment),
            ProjectRecommendedWindows(result),
            rootRisks,
            hourly,
            new MarineAnalysisExplanationResponse(
                ToApiName(explanation.Source),
                explanation.Degraded,
                explanation.Headline,
                explanation.Summary,
                explanation.ActivityNotes
                    .Select(note => new MarineAnalysisExplanationActivityNoteResponse(
                        ToApiName(note.Activity),
                        note.Text))
                    .ToArray(),
                explanation.RiskWindowText,
                explanation.UncertaintyText,
                explanation.Disclaimer,
                explanation.PromptVersion,
                explanation.ModelVersion,
                explanation.Locale),
            "结果仅供辅助决策，请以官方预警和现场管理为准。",
            traceId);
    }

    private static MarineAnalysisOverallResponse ProjectOverall(
        HourlyMarineAssessment assessment) => new(
            assessment.Score,
            ToApiName(assessment.RiskLevel),
            assessment.Confidence,
            assessment.AlgorithmVersion);

    private static MarineAnalysisActivityResponse[] ProjectActivities(
        HourlyMarineAssessment assessment) =>
        assessment.ActivityAssessments
            .Select(activity => new MarineAnalysisActivityResponse(
                ToApiName(activity.ActivityType),
                activity.Score,
                ToApiName(activity.RiskLevel),
                activity.Confidence,
                activity.AlgorithmVersion))
            .ToArray();

    private static MarineAnalysisRecommendedWindowResponse[] ProjectRecommendedWindows(
        MarineAnalysisQueryResult result) =>
        result.RecommendedWindows
            .Select(window => new MarineAnalysisRecommendedWindowResponse(
                ToApiName(window.ActivityType),
                window.StartUtc,
                window.EndUtc,
                window.ReturnBeforeUtc,
                window.RiskRisesAtUtc,
                window.RiskReason,
                window.BestScore,
                window.MinimumScore,
                window.DurationHours))
            .ToArray();

    private static MarineAnalysisRiskResponse[] ProjectRisks(
        HourlyMarineAssessment assessment) =>
        assessment.Contributions
            .Where(contribution => contribution.Penalty > 0)
            .Select(contribution => new MarineAnalysisRiskResponse(
                contribution.Code,
                ToApiName(contribution.Kind),
                ToApiName(contribution.Severity),
                assessment.ForecastTimeUtc,
                contribution.Metric,
                contribution.Actual,
                contribution.Threshold,
                contribution.Penalty,
                contribution.Message))
            .ToArray();

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

    private static bool IsNotModified(HttpContext httpContext, string etag)
    {
        if (!httpContext.Request.Headers.TryGetValue("If-None-Match", out var candidates))
        {
            return false;
        }

        return candidates
            .SelectMany(value => value?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [])
            .Any(candidate => string.Equals(candidate, etag, StringComparison.Ordinal) || candidate == "*");
    }

    private static IResult CreateLocationNotFoundProblem(
        Guid locationId,
        string traceId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Location was not found.",
            detail: $"No location exists for id '{locationId}'.",
            type: "https://marine-insight.local/problems/location-not-found",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = MarineInsightErrorCodes.LocationNotFound,
                ["traceId"] = traceId
            });

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
