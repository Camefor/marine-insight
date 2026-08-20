using System.Security.Claims;
using MarineInsight.Application.Admin;
using MarineInsight.Application.Credentials;
using MarineInsight.Application.Errors;
using MarineInsight.Domain.Location;
using MarineInsight.Infrastructure.Providers.WorldTides;
using MarineInsight.Web.Admin;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;

namespace MarineInsight.Web.Api;

public static class AdminEndpointExtensions
{
    private const string WorldTidesProviderName = "worldtides";

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
        group.MapGet("/providers/worldtides/credentials", ListCredentialsAsync);
        group.MapPost("/providers/worldtides/credentials", AddCredentialAsync).AddEndpointFilter(ValidateAntiforgeryAsync);
        group.MapPut("/providers/worldtides/credentials/{id:guid}/activate", ActivateCredentialAsync).AddEndpointFilter(ValidateAntiforgeryAsync);
        group.MapDelete("/providers/worldtides/credentials/{id:guid}", DeleteCredentialAsync).AddEndpointFilter(ValidateAntiforgeryAsync);
        group.MapPost("/providers/worldtides/credentials/test", TestCredentialAsync).AddEndpointFilter(ValidateAntiforgeryAsync);

        return endpoints;
    }

    private static async Task<IResult> ListCredentialsAsync(ProviderCredentialService service, CancellationToken cancellationToken) =>
        Results.Ok((await service.ListAsync(WorldTidesProviderName, cancellationToken)).Select(Project).ToArray());

    private static async Task<IResult> AddCredentialAsync(
        ClaimsPrincipal user,
        UpdateWorldTidesCredentialRequest request,
        ProviderCredentialService service,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.AddAsync(GetUserId(user), WorldTidesProviderName, request.ApiKey, cancellationToken);
            return Results.Ok((await service.ListAsync(WorldTidesProviderName, cancellationToken)).Select(Project).ToArray());
        }
        catch (ProviderCredentialConflictException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Credential already exists.",
                detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = exception.ErrorCode });
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.Message);
        }
    }

    private static async Task<IResult> ActivateCredentialAsync(
        ClaimsPrincipal user,
        Guid id,
        ProviderCredentialService service,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.SetActiveAsync(GetUserId(user), WorldTidesProviderName, id, cancellationToken);
            return Results.Ok();
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.Message);
        }
    }

    private static async Task<IResult> DeleteCredentialAsync(
        ClaimsPrincipal user,
        Guid id,
        ProviderCredentialService service,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.DeleteAsync(GetUserId(user), WorldTidesProviderName, id, cancellationToken);
            return Results.Ok();
        }
        catch (ProviderCredentialInUseException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Credential is in use.",
                detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = exception.ErrorCode });
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.Message);
        }
    }

    private static async Task<IResult> TestCredentialAsync(
        UpdateWorldTidesCredentialRequest request,
        WorldTidesProvider provider,
        CancellationToken cancellationToken)
    {
        var result = await provider.ValidateKeyAsync(request.ApiKey, cancellationToken);
        return Results.Ok(new WorldTidesKeyTestResponse(result.Success, result.Message, result.RemainingCredits));
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
        request.CoastOrientationDeg,
        request.IsHomeDefault);

    private static UpdateLocationCommand ToUpdateCommand(UpdateAdminLocationRequest request) => new(
        request.DisplayName,
        request.Latitude,
        request.Longitude,
        request.TimeZoneId,
        ParseLocationType(request.LocationType),
        request.CoastOrientationDeg,
        request.IsHomeDefault);

    private static LocationType ParseLocationType(string? value)
    {
        if (!Enum.TryParse<LocationType>(value, ignoreCase: true, out var type) || type == LocationType.Unknown)
        {
            throw new ArgumentException("Location type is required and must be a known value.");
        }

        return type;
    }

    private static WorldTidesCredentialResponse Project(ProviderCredentialSummary summary) => new(
        summary.Id,
        summary.KeyHint,
        summary.IsActive,
        ToApiName(summary.Health),
        summary.RemainingCredits,
        summary.CreditWarning,
        summary.LastCheckedAtUtc,
        summary.LastFailureReason,
        summary.UpdatedAtUtc);

    private static AdminLocationResponse Project(Location location) => new(
        location.Id,
        location.DisplayName,
        ToApiName(location.LocationType),
        location.Latitude,
        location.Longitude,
        location.TimeZoneId,
        location.CoastOrientationDeg,
        location.IsHomeDefault,
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
    double? CoastOrientationDeg,
    bool IsHomeDefault = false);

public sealed record UpdateAdminLocationRequest(
    string DisplayName,
    double Latitude,
    double Longitude,
    string TimeZoneId,
    string LocationType,
    double? CoastOrientationDeg,
    bool IsHomeDefault = false);

public sealed record AdminLocationResponse(
    Guid Id,
    string DisplayName,
    string LocationType,
    double Latitude,
    double Longitude,
    string TimeZoneId,
    double? CoastOrientationDeg,
    bool IsHomeDefault,
    DateTimeOffset CreatedAtUtc);

public sealed record LocationDeleteResponse(
    bool Deleted,
    int CascadedFavoriteCount);

public sealed record UpdateWorldTidesCredentialRequest(
    string ApiKey);

public sealed record WorldTidesCredentialResponse(
    Guid Id,
    string KeyHint,
    bool IsActive,
    string Health,
    int? RemainingCredits,
    bool CreditWarning,
    DateTimeOffset? LastCheckedAtUtc,
    string? LastFailureReason,
    DateTimeOffset UpdatedAtUtc);

public sealed record WorldTidesKeyTestResponse(
    bool Success,
    string Message,
    int? RemainingCredits);
