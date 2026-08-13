using System.Security.Claims;
using MarineInsight.Application.Users;
using MarineInsight.Domain.Analysis;
using Microsoft.AspNetCore.Antiforgery;

namespace MarineInsight.Web.Api;

public static class UserWorkspaceEndpointExtensions
{
    public static IEndpointRouteBuilder MapUserWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").RequireAuthorization();

        group.MapGet("/favorites", async (ClaimsPrincipal user, UserWorkspaceService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListFavoritesAsync(GetUserId(user), cancellationToken)));
        group.MapPost("/favorites", AddFavoriteAsync).AddEndpointFilter(ValidateAntiforgeryAsync);
        group.MapPut("/favorites/{favoriteId:guid}", UpdateFavoriteAsync).AddEndpointFilter(ValidateAntiforgeryAsync);
        group.MapDelete("/favorites/{favoriteId:guid}", DeleteFavoriteAsync).AddEndpointFilter(ValidateAntiforgeryAsync);
        group.MapGet("/query-history", async (ClaimsPrincipal user, UserWorkspaceService service, int? limit, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListHistoryAsync(GetUserId(user), limit ?? 50, cancellationToken)));
        group.MapGet("/user-settings", async (ClaimsPrincipal user, UserWorkspaceService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetSettingsAsync(GetUserId(user), cancellationToken)));
        group.MapPut("/user-settings", SaveSettingsAsync).AddEndpointFilter(ValidateAntiforgeryAsync);
        return endpoints;
    }

    private static async Task<IResult> AddFavoriteAsync(
        ClaimsPrincipal user,
        FavoriteRequest request,
        UserWorkspaceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var favorite = await service.AddFavoriteAsync(GetUserId(user), ToCommand(request), cancellationToken);
            return Results.Created($"/api/v1/favorites/{favorite.Id}", favorite);
        }
        catch (FavoriteAlreadyExistsException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Favorite already exists.", detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = "FAVORITE_ALREADY_EXISTS" });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Location not found.", detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = "LOCATION_NOT_FOUND" });
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.Message);
        }
    }

    private static async Task<IResult> UpdateFavoriteAsync(
        ClaimsPrincipal user,
        Guid favoriteId,
        FavoriteRequest request,
        UserWorkspaceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var favorite = await service.UpdateFavoriteAsync(GetUserId(user), favoriteId, ToCommand(request), cancellationToken);
            return favorite is null ? Results.NotFound() : Results.Ok(favorite);
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.Message);
        }
    }

    private static async Task<IResult> DeleteFavoriteAsync(
        ClaimsPrincipal user,
        Guid favoriteId,
        UserWorkspaceService service,
        CancellationToken cancellationToken) =>
        await service.DeleteFavoriteAsync(GetUserId(user), favoriteId, cancellationToken) ? Results.NoContent() : Results.NotFound();

    private static async Task<IResult> SaveSettingsAsync(
        ClaimsPrincipal user,
        UserSettingsRequest request,
        UserWorkspaceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.SaveSettingsAsync(
                GetUserId(user),
                new UserSettings(request.WindSpeedUnit, request.WaveHeightUnit, request.TemperatureUnit,
                    ParseActivity(request.DefaultActivity), request.TimeZoneId),
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.Message);
        }
    }

    internal static Guid GetUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new InvalidOperationException("The authenticated principal has no valid user id.");

    private static async ValueTask<object?> ValidateAntiforgeryAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        await antiforgery.ValidateRequestAsync(context.HttpContext);
        return await next(context);
    }

    private static SaveFavoriteCommand ToCommand(FavoriteRequest request) =>
        new(request.LocationId, ParseActivity(request.DefaultActivity), request.Note, request.SortOrder);

    private static ActivityType? ParseActivity(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : Enum.TryParse<ActivityType>(value, true, out var activity)
            ? activity
            : throw new ArgumentException("Default activity is unsupported.");

    private static IResult Validation(string detail) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Request validation failed.",
        detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = "VALIDATION_FAILED" });
}

public sealed record FavoriteRequest(Guid LocationId, string? DefaultActivity, string? Note, int SortOrder);

public sealed record UserSettingsRequest(
    string WindSpeedUnit,
    string WaveHeightUnit,
    string TemperatureUnit,
    string? DefaultActivity,
    string? TimeZoneId);
