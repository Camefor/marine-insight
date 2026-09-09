using System.Text.Json;

using MarineInsight.Application.Sharing;
using MarineInsight.Web.Components.Features.Dashboard;

namespace MarineInsight.Web.Api;

public static class ShareEndpointExtensions
{
    public static IEndpointRouteBuilder MapShareEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1");
        group.MapPost("/shares", CreateAsync)
            .AllowAnonymous()
            .RequireRateLimiting("analysis")
            .Produces<ShareLinkResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);
        group.MapGet("/shares/{token}", GetAsync)
            .AllowAnonymous()
            .RequireRateLimiting("location")
            .Produces<ShareSnapshotResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateShareRequest? request,
        ShareSnapshotService service,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
            {
                throw new ArgumentException("Share payload is required.", nameof(request));
            }

            var result = JsonSerializer.Deserialize<DashboardAnalysisResult>(request.Payload);
            if (result is null)
            {
                throw new JsonException("Share payload must contain a marine conditions result.");
            }

            var payload = JsonSerializer.Serialize(result);
            var created = await service.CreateAsync(payload, cancellationToken);
            var url = $"{httpRequest.Scheme}://{httpRequest.Host}/share/{created.Token}";
            return Results.Created($"/api/v1/shares/{created.Token}", new ShareLinkResponse(created.Token, url, created.ExpiresAtUtc));
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["payload"] = [exception.Message] });
        }
        catch (JsonException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["payload"] = [exception.Message] });
        }
    }

    private static async Task<IResult> GetAsync(
        string token,
        ShareSnapshotService service,
        CancellationToken cancellationToken)
    {
        var snapshot = await service.GetAsync(token, cancellationToken);
        return snapshot is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Share link was not found or has expired.",
                detail: "The shared marine conditions snapshot is no longer available.",
                extensions: new Dictionary<string, object?> { ["code"] = "SHARE_NOT_FOUND" })
            : Results.Ok(new ShareSnapshotResponse(snapshot.Token, snapshot.Payload, snapshot.CreatedAtUtc, snapshot.ExpiresAtUtc));
    }
}

public sealed record CreateShareRequest(string Payload);

public sealed record ShareLinkResponse(string Token, string Url, DateTimeOffset ExpiresAtUtc);

public sealed record ShareSnapshotResponse(
    string Token,
    string Payload,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);
