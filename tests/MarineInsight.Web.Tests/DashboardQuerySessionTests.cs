using System.Net;
using MarineInsight.Application.Users;
using MarineInsight.Domain.Analysis;
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
        Assert.Contains("地图选点", html, StringComparison.Ordinal);
        Assert.Contains("天地图", html, StringComparison.Ordinal);
        Assert.Contains("Open-Meteo.com", html, StringComparison.Ordinal);
        Assert.Contains("纬度", html, StringComparison.Ordinal);
        Assert.Contains("经度", html, StringComparison.Ordinal);
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
        session.ForecastStartLocal = new DateTime(2026, 7, 16, 0, 0, 0);
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
        Assert.Equal(3, session.Result.TrendTabs.Count);
        Assert.Contains(session.Result.TrendTabs, tab => tab.Key == DashboardTrendKeys.Score && tab.Points.Count == 25);
        Assert.Contains(session.Result.TrendTabs, tab => tab.Key == DashboardTrendKeys.Wind && tab.Points.Count == 25);
        Assert.Contains(session.Result.TrendTabs, tab => tab.Key == DashboardTrendKeys.Wave && tab.Points.Count == 25);
        Assert.NotEmpty(session.Result.TimelineWindows);
        Assert.Equal(session.Result.HourlyDetails[0].ForecastTimeUtc, session.SelectedHourlyDetail?.ForecastTimeUtc);

        session.SelectTrend(DashboardTrendKeys.Wave);
        session.SelectHour(session.Result.HourlyDetails[1].ForecastTimeUtc);

        Assert.Equal(DashboardTrendKeys.Wave, session.SelectedTrendKey);
        Assert.Equal(session.Result.HourlyDetails[1].ForecastTimeUtc, session.SelectedHourlyDetail?.ForecastTimeUtc);
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
        session.ForecastStartLocal = new DateTime(2026, 7, 16, 0, 0, 0);

        await session.SearchLocationsAsync();
        session.SelectLocation(session.LocationResults.Single().Id);
        await session.SubmitAnalysisAsync();

        Assert.Null(session.Result);
        Assert.Contains("天气数据源暂时不可用", session.AnalysisError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UserSettingsAndRepeatQueryContextAffectProjectionAndRequestedActivity()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        await factory.MigrateDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();
        var repeatedFrom = new DateTimeOffset(2026, 7, 16, 3, 0, 0, TimeSpan.Zero);
        session.ApplySettings(new UserSettings("knot", "foot", "fahrenheit", ActivityType.Boat, "Asia/Shanghai"));
        session.ApplyQueryContext(repeatedFrom, ActivityType.Landing);
        session.Hours = 24;

        Assert.True(await session.SelectCatalogLocationAsync(Guid.Parse("8a477d67-73fa-4f43-b954-cd29d238a89d")));
        await session.SubmitAnalysisAsync();

        Assert.Equal(new DateTime(2026, 7, 16, 11, 0, 0), session.ForecastStartLocal);
        Assert.Equal([ActivityType.Landing], session.RequestedActivities);
        Assert.Contains(session.Result!.MetricCards, metric => metric.Label == "风速" && metric.Unit == "kn");
        Assert.Contains(session.Result.MetricCards, metric => metric.Label == "有效波高" && metric.Unit == "ft");
        Assert.Single(session.Result.ActivityScores);
        Assert.Equal("landing", session.Result.ActivityScores[0].Type);
    }

    [Fact]
    public async Task MapPointSelectionSubmitsCoordinateAnalysisWithoutCatalogLocation()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();

        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();
        session.ForecastStartLocal = new DateTime(2026, 7, 16, 0, 0, 0);
        session.Hours = 24;

        Assert.True(session.SelectMapPoint(30.194, 122.687));
        await session.SubmitAnalysisAsync();

        Assert.Null(session.AnalysisError);
        Assert.NotNull(session.Result);
        Assert.Equal("自定义坐标", session.Result.DisplayName);
        Assert.Equal(30.194, session.Result.Latitude, 3);
        Assert.Equal(122.687, session.Result.Longitude, 3);
        Assert.Equal(2, session.Result.Sources.Count);
        Assert.Equal(25, session.Result.HourlyRows.Count);
    }

    [Fact]
    public async Task InvalidMapPointLeavesCoordinateFallbackErrorAndBlocksSubmit()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();

        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();

        Assert.False(session.SelectMapPoint(91, 122.687));
        Assert.False(session.CanSubmit);
        Assert.Contains("纬度", session.MapError, StringComparison.Ordinal);

        await session.SubmitAnalysisAsync();

        Assert.Null(session.Result);
        Assert.Contains("地图/坐标", session.AnalysisError, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectMapPointRoundsCoordinatesToSixDecimalPlaces()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();

        Assert.True(session.SelectMapPoint(30.1941234567, 122.6879876543));

        Assert.Equal(30.194123, session.MapLatitude, 6);
        Assert.Equal(122.687988, session.MapLongitude, 6);
        Assert.NotNull(session.SelectedMapPoint);
        Assert.Equal(30.194123, session.SelectedMapPoint.Latitude, 6);
        Assert.Equal(122.687988, session.SelectedMapPoint.Longitude, 6);
    }

    [Fact]
    public void ClientTimeZoneDefaultsToBeijingAndSwitchesOnDetection()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();

        Assert.Equal("北京时间（UTC+8）", session.DisplayTimeZoneLabel);

        Assert.True(session.SetClientTimeZone("America/New_York"));
        Assert.StartsWith("纽约时间", session.DisplayTimeZoneLabel);

        Assert.True(session.SetClientTimeZone(null));
        Assert.Equal("北京时间（UTC+8）", session.DisplayTimeZoneLabel);

        Assert.False(session.SetClientTimeZone("Asia/Shanghai"));
    }
}
