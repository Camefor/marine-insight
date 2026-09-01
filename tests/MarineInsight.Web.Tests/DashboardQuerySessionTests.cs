using System.Net;
using MarineInsight.Application.Users;
using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Persistence;
using MarineInsight.Web.Components.Features.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace MarineInsight.Web.Tests;

public sealed class DashboardQuerySessionTests
{
    [Fact]
    public async Task RootDashboardRendersQueryShell()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Marine AI | 海岛海况智能决策平台", html, StringComparison.Ordinal);
        Assert.Equal(1, html.Split("<title>", StringSplitOptions.None).Length - 1);
        Assert.Contains("id=\"dashboard-title\"", html, StringComparison.Ordinal);
        Assert.Contains("海岛海况智能决策平台</h1>", html, StringComparison.Ordinal);
        Assert.Contains("为海钓、露营、摄影和航海而生。", html, StringComparison.Ordinal);
        Assert.Contains("name=\"description\"", html, StringComparison.Ordinal);
        Assert.Contains("/images/brand/marine-ai-logo.svg", html, StringComparison.Ordinal);
        Assert.Contains("class=\"ui-icon\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">⌂<", html, StringComparison.Ordinal);
        Assert.Contains("地点搜索", html, StringComparison.Ordinal);
        Assert.Contains("登录后可查询潮汐", html, StringComparison.Ordinal);
        Assert.Contains("forecast-time-picker", html, StringComparison.Ordinal);
        Assert.Contains("_content/AntDesign/css/ant-design-blazor.css", html, StringComparison.Ordinal);
        Assert.Contains("等待查询", html, StringComparison.Ordinal);
        Assert.Contains("Open-Meteo.com", html, StringComparison.Ordinal);
        // 需求1：地图默认收起，选点面板内容（含“地图选点/天地图/纬度/经度”）不预渲染。
        Assert.DoesNotContain("map-picker-title", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeoDiscoveryFilesAndBrandAssetsAreServed()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        using var client = factory.CreateClient();

        var robots = await client.GetStringAsync("/robots.txt");
        var sitemap = await client.GetStringAsync("/sitemap.xml");
        var manifest = await client.GetStringAsync("/site.webmanifest");
        var brandAssets = new Dictionary<string, string>
        {
            ["/images/brand/marine-ai-logo.svg"] = "image/svg+xml",
            ["/images/brand/marine-ai-mark.svg"] = "image/svg+xml",
            ["/images/brand/favicon-32.png"] = "image/png",
            ["/images/brand/favicon.ico"] = "image/x-icon",
            ["/images/brand/apple-touch-icon.png"] = "image/png",
            ["/images/brand/marine-ai-mark-192.png"] = "image/png",
            ["/images/brand/marine-ai-mark-512.png"] = "image/png"
        };

        Assert.Contains("Sitemap: https://marine.loyalme.life/sitemap.xml", robots, StringComparison.Ordinal);
        Assert.Contains("https://marine.loyalme.life/about", sitemap, StringComparison.Ordinal);
        Assert.Contains("Marine AI", manifest, StringComparison.Ordinal);
        Assert.Contains("#0A131F", manifest, StringComparison.Ordinal);
        Assert.Contains("\"purpose\": \"any maskable\"", manifest, StringComparison.Ordinal);
        foreach (var (assetPath, contentType) in brandAssets)
        {
            using var assetResponse = await client.GetAsync(assetPath);
            Assert.Equal(HttpStatusCode.OK, assetResponse.StatusCode);
            Assert.Equal(contentType, assetResponse.Content.Headers.ContentType?.MediaType);
            Assert.True(assetResponse.Content.Headers.ContentLength > 0);
        }
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
        Assert.True(session.IsMapPickerOpen);
        Assert.Equal(location.Latitude, session.MapLatitude);
        Assert.Equal(location.Longitude, session.MapLongitude);
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
        Assert.Equal("dry", session.Result.WeatherSummary.Status);
        Assert.Equal("当前无雨", session.Result.WeatherSummary.StatusText);
        Assert.Equal("0.0 mm/h", session.Result.WeatherSummary.RainAmount);
        Assert.Equal("4.0 m/s", session.Result.WeatherSummary.WindSpeed);
        Assert.Equal("6.0 m/s", session.Result.WeatherSummary.WindGust);
        Assert.Equal("3级（微风）", session.Result.WeatherSummary.WindForce);
        Assert.Null(session.Result.WeatherSummary.RainStartUtc);
        Assert.Equal("not_requested", session.Result.Tide.Status);
        Assert.Empty(session.Result.Tide.Points);
    }

    [Fact]
    public async Task SearchWithoutPresetExpandsMapAndKeepsGuidanceError()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        await factory.MigrateDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();
        session.SearchText = "不存在的地点";

        await session.SearchLocationsAsync();

        Assert.True(session.IsMapPickerOpen);
        Assert.Empty(session.LocationResults);
        Assert.Null(session.SelectedLocation);
        Assert.Equal("没有找到匹配的预置地点，请通过地图选点或输入经纬度继续。", session.SearchError);
    }

    [Fact]
    public async Task WeatherSummaryProjectsCurrentRainAndReliableEnd()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        factory.Weather.MetricsFactory = index => ForecastMetricSet.Create(
            windSpeedMs: 8.2,
            windGustMs: 12.4,
            precipitationMmPerHour: index <= 2 ? 1.25 : 0);

        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();
        session.ForecastStartLocal = new DateTime(2026, 7, 16, 8, 0, 0);
        session.Hours = 24;

        Assert.True(session.SelectMapPoint(30.194, 122.687));
        await session.SubmitAnalysisAsync();

        var summary = Assert.IsType<DashboardWeatherSummary>(session.Result?.WeatherSummary);
        Assert.Equal("raining", summary.Status);
        Assert.Equal("当前下雨", summary.StatusText);
        Assert.Equal("1.3 mm/h", summary.RainAmount);
        Assert.Equal("8.2 m/s", summary.WindSpeed);
        Assert.Equal("12.4 m/s", summary.WindGust);
        Assert.Equal("5级（清风）", summary.WindForce);
        Assert.Equal(session.Result!.FromUtc, summary.RainStartUtc);
        Assert.Equal(session.Result.FromUtc.AddHours(3), summary.RainEndUtc);
        Assert.True(summary.RainEndWithinQuery);
    }

    [Fact]
    public async Task WeatherSummaryProjectsFutureRainWindow()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        factory.Weather.MetricsFactory = index => ForecastMetricSet.Create(
            windSpeedMs: 4,
            windGustMs: 6,
            precipitationMmPerHour: index is 2 or 3 ? 0.8 : 0);

        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();
        session.ForecastStartLocal = new DateTime(2026, 7, 16, 8, 0, 0);
        session.Hours = 24;

        Assert.True(session.SelectMapPoint(30.194, 122.687));
        await session.SubmitAnalysisAsync();

        var summary = Assert.IsType<DashboardWeatherSummary>(session.Result?.WeatherSummary);
        Assert.Equal("dry", summary.Status);
        Assert.Equal(session.Result!.FromUtc.AddHours(2), summary.RainStartUtc);
        Assert.Equal(session.Result.FromUtc.AddHours(4), summary.RainEndUtc);
        Assert.Contains("稍后有降雨", summary.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WeatherSummaryKeepsEndUnknownWhenRainContinuesThroughQueryWindow()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        factory.Weather.MetricsFactory = _ => ForecastMetricSet.Create(
            windSpeedMs: 4,
            windGustMs: 6,
            precipitationMmPerHour: 0.5);

        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();
        session.ForecastStartLocal = new DateTime(2026, 7, 16, 8, 0, 0);
        session.Hours = 24;

        Assert.True(session.SelectMapPoint(30.194, 122.687));
        await session.SubmitAnalysisAsync();

        var summary = Assert.IsType<DashboardWeatherSummary>(session.Result?.WeatherSummary);
        Assert.Equal("raining", summary.Status);
        Assert.Equal(session.Result!.FromUtc, summary.RainStartUtc);
        Assert.Null(summary.RainEndUtc);
        Assert.False(summary.RainEndWithinQuery);
        Assert.Contains("未发现可靠的结束点", summary.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WeatherSummaryDoesNotInventRainStateOrEndFromMissingData()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        factory.Weather.MetricsFactory = index => ForecastMetricSet.Create(
            windSpeedMs: 4,
            precipitationMmPerHour: index == 1 ? 1 : null);

        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();
        session.ForecastStartLocal = new DateTime(2026, 7, 16, 8, 0, 0);
        session.Hours = 24;

        Assert.True(session.SelectMapPoint(30.194, 122.687));
        await session.SubmitAnalysisAsync();

        var summary = Assert.IsType<DashboardWeatherSummary>(session.Result?.WeatherSummary);
        Assert.Equal("unknown", summary.Status);
        Assert.Equal(session.Result!.FromUtc.AddHours(1), summary.RainStartUtc);
        Assert.Null(summary.RainEndUtc);
        Assert.False(summary.RainEndWithinQuery);
        Assert.Contains("无法可靠判断", summary.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TideProjectionIncludesChartPointsExtremesAndTrend()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        factory.Tide.IsEnabled = true;
        await factory.MigrateDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MarineInsightDbContext>();
        var actor = new MarineInsightUser
        {
            Id = Guid.NewGuid(),
            UserName = "tide-user@example.com",
            Email = "tide-user@example.com"
        };
        dbContext.Users.Add(actor);
        await dbContext.SaveChangesAsync();
        session.ForecastStartLocal = new DateTime(2026, 7, 16, 0, 0, 0);
        session.Hours = 24;
        session.IncludeTide = true;

        Assert.True(await session.SelectCatalogLocationAsync(Guid.Parse("8a477d67-73fa-4f43-b954-cd29d238a89d")));
        await session.SubmitAnalysisAsync(actor.Id);

        var tide = Assert.IsType<DashboardTideResult>(session.Result?.Tide);
        Assert.Equal("available", tide.Status);
        Assert.Equal("涨潮", tide.CurrentTrendText);
        Assert.Equal(25, tide.Points.Count);
        Assert.Equal("high", tide.NextHigh?.Type);
        Assert.Equal("low", tide.NextLow?.Type);
        Assert.True(tide.MinimumHeightM < tide.MaximumHeightM);
        Assert.Contains(session.Result!.Sources, source => source.DataDomain == "tide" && source.CacheStatus == "miss");
        Assert.Contains(session.Result.HourlyDetails.SelectMany(detail => detail.Metrics), metric => metric.Label == "潮位");
    }

    [Fact]
    public async Task SubmitWithNonUtcBoundaryTimeReturnsActionableMinuteHint()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();
        session.ForecastStartLocal = new DateTime(2026, 7, 16, 8, 30, 0);

        Assert.True(await session.SelectCatalogLocationAsync(Guid.Parse("8a477d67-73fa-4f43-b954-cd29d238a89d")));
        await session.SubmitAnalysisAsync();

        Assert.Null(session.Result);
        Assert.Equal("起报时间需选择整点（分钟 00），才能查询 UTC 整点预报。", session.AnalysisError);
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
    public async Task MapPointCustomNameFlowsIntoResultDisplayName()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();

        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<DashboardQuerySession>();
        session.ForecastStartLocal = new DateTime(2026, 7, 16, 0, 0, 0);
        session.Hours = 24;
        session.MapPointName = " 我的海钓点 ";

        Assert.True(session.SelectMapPoint(30.194, 122.687));
        await session.SubmitAnalysisAsync();

        Assert.Null(session.AnalysisError);
        Assert.NotNull(session.Result);
        Assert.Equal("我的海钓点", session.Result.DisplayName);
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
