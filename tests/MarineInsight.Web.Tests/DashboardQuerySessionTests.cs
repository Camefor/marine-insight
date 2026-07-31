using System.Net;
using MarineInsight.Web.Components.Features.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace MarineInsight.Web.Tests;

public sealed class DashboardQuerySessionTests
{
    [Fact]
    public async Task RootDashboardRendersQueryShell()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("海况 Dashboard", html, StringComparison.Ordinal);
        Assert.Contains("地点搜索", html, StringComparison.Ordinal);
        Assert.Contains("等待查询", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAndSubmitLoadsMetricsSourcesAndHourlyRows()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        await factory.MigrateDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();
        session.SearchText = "东极岛";
        session.ForecastStartUtc = new DateTime(2026, 7, 16, 0, 0, 0);
        session.Hours = 24;

        await session.SearchLocationsAsync();
        var location = Assert.Single(session.LocationResults);
        session.SelectLocation(location.Id);
        await session.SubmitAnalysisAsync();

        Assert.Null(session.SearchError);
        Assert.Null(session.AnalysisError);
        Assert.NotNull(session.Result);
        Assert.Equal("东极岛", session.Result.DisplayName);
        Assert.Equal(2, session.Result.Sources.Count);
        Assert.Equal(25, session.Result.HourlyRows.Count);
        Assert.NotNull(session.Result.Overall);
        Assert.Equal("适宜", session.Result.Overall.RiskLevelText);
        Assert.Equal(5, session.Result.ActivityScores.Count);
        Assert.Contains(session.Result.ActivityScores, activity => activity.Type == "boat" && activity.RiskLevelText == "适宜");
        Assert.NotEmpty(session.Result.RecommendationWindows);
        Assert.Contains(session.Result.RecommendationWindows, window => window.Activity == "boat");
        Assert.NotEmpty(session.Result.TopRisks);
        Assert.Contains(session.Result.MetricCards, metric => metric.Label == "风速" && metric.Value == "4.0");
        Assert.Contains(session.Result.MetricCards, metric => metric.Label == "有效波高" && metric.Value == "0.8");
    }

    [Fact]
    public async Task ProviderFailureLeavesActionableDashboardError()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        factory.Weather.ShouldFail = true;
        await factory.MigrateDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();
        session.SearchText = "东极岛";
        session.ForecastStartUtc = new DateTime(2026, 7, 16, 0, 0, 0);

        await session.SearchLocationsAsync();
        session.SelectLocation(session.LocationResults.Single().Id);
        await session.SubmitAnalysisAsync();

        Assert.Null(session.Result);
        Assert.Contains("天气数据源暂时不可用", session.AnalysisError, StringComparison.Ordinal);
    }
}
