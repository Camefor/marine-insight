using MarineInsight.Web.Operations;
using Microsoft.AspNetCore.RateLimiting;

namespace MarineInsight.Web.Api;

public static class OperationsEndpointExtensions
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/operations", async (
                OperationsOverviewService service,
                CancellationToken cancellationToken) => Results.Ok(await service.GetAsync(cancellationToken)))
            .RequireAuthorization("Administrator")
            .RequireRateLimiting("admin")
            .WithName("GetOperationsOverview");
        return endpoints;
    }
}
