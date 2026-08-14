using System.Diagnostics;
using MarineInsight.Application.Errors;
using MarineInsight.Application.Locations;
using MarineInsight.Domain.Forecast;
using MarineInsight.Domain.Location;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace MarineInsight.Web.Api;

public static class LocationEndpointExtensions
{
    public static IEndpointRouteBuilder MapLocationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/v1").AllowAnonymous().RequireRateLimiting("location");
        group.MapGet("/locations/search", HandleSearchAsync)
            .WithName("SearchLocations")
            .Produces<IReadOnlyList<LocationResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);
        group.MapGet("/locations/nearby", HandleNearbyAsync)
            .WithName("FindNearbyLocations")
            .Produces<IReadOnlyList<LocationResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> HandleSearchAsync(
        string? q,
        int? limit,
        LocationQueryService queryService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var traceId = GetTraceId(httpContext);
        httpContext.Response.Headers["Trace-Id"] = traceId;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(q))
        {
            errors["q"] = ["Search text is required."];
        }

        var effectiveLimit = limit ?? LocationQueryService.DefaultLimit;
        if (effectiveLimit is < 1 or > LocationQueryService.MaxLimit)
        {
            errors["limit"] = [$"Limit must be between 1 and {LocationQueryService.MaxLimit}."];
        }

        if (errors.Count > 0)
        {
            return CreateValidationProblem(errors, traceId);
        }

        try
        {
            var locations = await queryService.SearchPresetsAsync(q!, effectiveLimit, cancellationToken);
            return Results.Ok(locations.Select(Project).ToArray());
        }
        catch (ArgumentException exception)
        {
            errors["q"] = [exception.Message];
            return CreateValidationProblem(errors, traceId);
        }
    }

    private static async Task<IResult> HandleNearbyAsync(
        double? lat,
        double? lon,
        double? radiusKm,
        int? limit,
        LocationQueryService queryService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var traceId = GetTraceId(httpContext);
        httpContext.Response.Headers["Trace-Id"] = traceId;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!lat.HasValue)
        {
            errors["lat"] = ["Latitude is required."];
        }

        if (!lon.HasValue)
        {
            errors["lon"] = ["Longitude is required."];
        }

        var effectiveRadiusKm = radiusKm ?? LocationQueryService.DefaultNearbyRadiusKm;
        if (!double.IsFinite(effectiveRadiusKm) ||
            effectiveRadiusKm <= 0 ||
            effectiveRadiusKm > LocationQueryService.MaxNearbyRadiusKm)
        {
            errors["radiusKm"] = [
                $"Radius must be greater than 0 and no more than {LocationQueryService.MaxNearbyRadiusKm} km."
            ];
        }

        var effectiveLimit = limit ?? LocationQueryService.DefaultLimit;
        if (effectiveLimit is < 1 or > LocationQueryService.MaxLimit)
        {
            errors["limit"] = [$"Limit must be between 1 and {LocationQueryService.MaxLimit}."];
        }

        GeoPoint center = default;
        if (lat.HasValue && lon.HasValue)
        {
            try
            {
                center = new GeoPoint(lat.Value, lon.Value);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                errors["location"] = [exception.Message];
            }
        }

        if (errors.Count > 0)
        {
            return CreateValidationProblem(errors, traceId);
        }

        try
        {
            var locations = await queryService.FindNearbyPresetsAsync(
                center,
                effectiveRadiusKm,
                effectiveLimit,
                cancellationToken);
            return Results.Ok(locations.Select(Project).ToArray());
        }
        catch (ArgumentException exception)
        {
            errors["location"] = [exception.Message];
            return CreateValidationProblem(errors, traceId);
        }
    }

    private static LocationResponse Project(Location location) => new(
        location.Id,
        location.DisplayName,
        ToApiName(location.LocationType),
        location.Latitude,
        location.Longitude,
        location.TimeZoneId,
        location.IsPreset ? "preset" : "catalog");

    private static IResult CreateValidationProblem(
        Dictionary<string, string[]> errors,
        string traceId) =>
        Results.ValidationProblem(
            errors,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Request validation failed.",
            type: "https://marine-insight.local/problems/validation-failed",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = MarineInsightErrorCodes.ValidationFailed,
                ["traceId"] = traceId
            });

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
}
