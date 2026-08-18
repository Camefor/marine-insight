using System.Security.Claims;
using MarineInsight.Application.Admin;
using MarineInsight.Application.Errors;
using MarineInsight.Domain.Location;
using MarineInsight.Web.Admin;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;

namespace MarineInsight.Web.Api;

public static class AdminEndpointExtensions
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin")
            .RequireAuthorization("Administrator")
            .RequireRateLimiting("admin");

        group.MapGet("/locations", async (AdminLocationService service, CancellationToken cancellationToken) =>
            Results.Ok((await service.ListPresetsAsync(cancellationToken)).Select(Project).ToArray()));
        group.MapPost("/locations", CreateLocationAsync).AddEndpointFilter(ValidateAntiforgeryAsync);
        group.MapPut("/locations/{id:guid}", UpdateLocationAsync).AddEndpointFilter(ValidateAntiforgeryAsync);
        group.MapDelete("/locations/{id:guid}", DeleteLocationAsync).AddEndpointFilter(ValidateAntiforgeryAsync);
        group.MapGet("/users", async (AdminUserService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListUsersAsync(cancellationToken)));

        return endpoints;
    }

    private static async Task<IResult> CreateLocationAsync(
        ClaimsPrincipal user,
        CreateAdminLocationRequest request,
        AdminLocationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var location = await service.CreateAsync(GetUserId(user), ToCreateCommand(request), cancellationToken);
            return Results.Created($"/api/v1/admin/locations/{location.Id}", Project(location));
        }
        catch (AdminLocationConflictException exception)
        {
            return Conflict(exception);
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.Message);
        }
    }

    private static async Task<IResult> UpdateLocationAsync(
        ClaimsPrincipal user,
        Guid id,
        UpdateAdminLocationRequest request,
        AdminLocationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var location = await service.UpdateAsync(GetUserId(user), id, ToUpdateCommand(request), cancellationToken);
            return location is null ? Results.NotFound() : Results.Ok(Project(location));
        }
        catch (AdminLocationConflictException exception)
        {
            return Conflict(exception);
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.Message);
        }
    }

    private static async Task<IResult> DeleteLocationAsync(
        ClaimsPrincipal user,
        Guid id,
        AdminLocationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.DeleteAsync(GetUserId(user), id, cancellationToken);
            return result is null
                ? Results.NotFound()
                : Results.Ok(new LocationDeleteResponse(result.Deleted, result.CascadedFavoriteCount));
        }
        catch (AdminLocationInUseException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Location is in use.",
                detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = exception.ErrorCode });
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.Message);
        }
    }

    private static CreateLocationCommand ToCreateCommand(CreateAdminLocationRequest request) => new(
        request.DisplayName,
        request.Latitude,
        request.Longitude,
        request.TimeZoneId,
        ParseLocationType(request.LocationType),
        request.CoastOrientationDeg);

    private static UpdateLocationCommand ToUpdateCommand(UpdateAdminLocationRequest request) => new(
        request.DisplayName,
        request.Latitude,
        request.Longitude,
        request.TimeZoneId,
        ParseLocationType(request.LocationType),
        request.CoastOrientationDeg);

    private static LocationType ParseLocationType(string? value)
    {
        if (!Enum.TryParse<LocationType>(value, ignoreCase: true, out var type) || type == LocationType.Unknown)
        {
            throw new ArgumentException("Location type is required and must be a known value.");
        }

        return type;
    }

    private static AdminLocationResponse Project(Location location) => new(
        location.Id,
        location.DisplayName,
        ToApiName(location.LocationType),
        location.Latitude,
        location.Longitude,
        location.TimeZoneId,
        location.CoastOrientationDeg,
        location.CreatedAtUtc);

    private static IResult Conflict(MarineInsightException exception) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Location conflict.",
            detail: exception.Message,
            extensions: new Dictionary<string, object?> { ["code"] = exception.ErrorCode });

    private static IResult Validation(string detail) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Request validation failed.",
        detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = "VALIDATION_FAILED" });

    private static async ValueTask<object?> ValidateAntiforgeryAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        await antiforgery.ValidateRequestAsync(context.HttpContext);
        return await next(context);
    }

    private static Guid GetUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new InvalidOperationException("The authenticated principal has no valid user id.");

    private static string ToApiName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        return name.Length == 0
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
    }
}

public sealed record CreateAdminLocationRequest(
    string DisplayName,
    double Latitude,
    double Longitude,
    string TimeZoneId,
    string LocationType,
    double? CoastOrientationDeg);

public sealed record UpdateAdminLocationRequest(
    string DisplayName,
    double Latitude,
    double Longitude,
    string TimeZoneId,
    string LocationType,
    double? CoastOrientationDeg);

public sealed record AdminLocationResponse(
    Guid Id,
    string DisplayName,
    string LocationType,
    double Latitude,
    double Longitude,
    string TimeZoneId,
    double? CoastOrientationDeg,
    DateTimeOffset CreatedAtUtc);

public sealed record LocationDeleteResponse(
    bool Deleted,
    int CascadedFavoriteCount);
