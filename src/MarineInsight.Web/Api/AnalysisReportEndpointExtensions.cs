using System.Security.Claims;
using MarineInsight.Application.Analysis;
using MarineInsight.Application.Errors;
using MarineInsight.Domain.Analysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MarineInsight.Web.Api;

public static class AnalysisReportEndpointExtensions
{
    public static IEndpointRouteBuilder MapAnalysisReportEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGroup("/api/v1")
            .RequireAuthorization()
            .MapGet("/marine-analyses/{id:guid}", HandleGetAsync)
            .Produces<MarineAnalysisReportResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> HandleGetAsync(
        Guid id,
        ClaimsPrincipal user,
        AnalysisReportService service,
        CancellationToken cancellationToken)
    {
        var report = await service.GetByIdAsync(
            UserWorkspaceEndpointExtensions.GetUserId(user),
            id,
            cancellationToken);

        return report is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Analysis report was not found.",
                detail: $"No analysis report exists for id '{id}'.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = MarineInsightErrorCodes.AnalysisNotFound
                })
            : Results.Ok(Project(report));
    }

    private static MarineAnalysisReportResponse Project(AnalysisReport report) => new(
        report.Id,
        new MarineAnalysisReportLocationResponse(report.LocationId),
        new MarineAnalysisRangeResponse(
            report.RangeStartUtc,
            report.RangeEndUtc,
            report.Hours),
        report.AlgorithmVersion,
        new MarineAnalysisOverallResponse(
            report.Score,
            ToApiName(report.RiskLevel),
            report.Confidence,
            report.AlgorithmVersion),
        report.Risks
            .Select(risk => new MarineAnalysisReportRiskResponse(
                risk.ForecastTimeUtc,
                risk.RuleCode,
                ToApiName(risk.Severity),
                risk.Actual,
                risk.Threshold,
                risk.Penalty,
                risk.Message))
            .ToArray(),
        report.SourceBatches
            .Select(source => new MarineAnalysisReportSourceResponse(
                source.BatchId,
                ToApiName(source.DataDomain),
                source.ProviderCode,
                source.SourceModel,
                ToApiName(source.SourceRole),
                source.SelectionPolicy))
            .ToArray(),
        report.RecommendedStartUtc is { } start && report.RecommendedEndUtc is { } end
            ? new MarineAnalysisReportRecommendedWindowResponse(start, end, report.ReturnBeforeUtc)
            : null,
        report.CreatedAtUtc);

    private static string ToApiName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        return name.Length == 0
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
    }
}
