using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarineInsight.Application.Analysis;
using MarineInsight.Application.Analysis.Ports;
using MarineInsight.Application.Errors;
using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Persistence;
using MarineInsight.Infrastructure.Providers.WorldTides;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MarineInsight.Web.Tests;

public sealed class MarineAnalysisApiTests
{
    private static readonly string[] UnsupportedActivities = ["diving"];

    [Fact]
    public void TestHostDisablesWorldTidesEvenWhenUserSecretsEnableIt()
    {
        using var factory = new ApiTestApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WorldTidesOptions>>();

        Assert.False(options.Value.Enabled);
    }

    [Fact]
    public async Task CoordinateQueryReturnsMetricsQualitySourcesAndCacheStatuses()
    {
        using var factory = new ApiTestApplicationFactory();
        using var client = factory.CreateClient();
        var request = CreateRequest();

        using var firstResponse = await client.PostAsJsonAsync("/api/v1/marine-analyses", request);
        using var firstDocument = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(firstResponse.Headers.GetValues("Trace-Id").Single()));
        var etag = firstResponse.Headers.GetValues("ETag").Single();
        Assert.StartsWith("\"", etag, StringComparison.Ordinal);
        Assert.Equal("analyzed", firstDocument.RootElement.GetProperty("analysisStatus").GetString());
        Assert.Equal("marine-score-1.0.0", firstDocument.RootElement.GetProperty("algorithmVersion").GetString());
        Assert.Equal(etag, firstDocument.RootElement.GetProperty("cache").GetProperty("eTag").GetString());
        Assert.Contains(
            ":marine-score-1.0.0:",
            firstDocument.RootElement.GetProperty("cache").GetProperty("key").GetString(),
            StringComparison.Ordinal);
        Assert.Equal("good", firstDocument.RootElement.GetProperty("overall").GetProperty("riskLevel").GetString());
        Assert.Equal(1, firstDocument.RootElement.GetProperty("activities").GetArrayLength());
        Assert.Equal("boat", firstDocument.RootElement.GetProperty("activities")[0].GetProperty("type").GetString());
        Assert.Equal(
            "marine-score-1.0.0",
            firstDocument.RootElement.GetProperty("activities")[0].GetProperty("algorithmVersion").GetString());
        Assert.True(firstDocument.RootElement.GetProperty("recommendedWindows").GetArrayLength() > 0);
        Assert.Equal(
            "boat",
            firstDocument.RootElement
                .GetProperty("recommendedWindows")[0]
                .GetProperty("activity")
                .GetString());
        Assert.True(firstDocument.RootElement.GetProperty("risks").GetArrayLength() > 0);
        Assert.Equal(2, firstDocument.RootElement.GetProperty("sources").GetArrayLength());
        Assert.All(
            firstDocument.RootElement.GetProperty("sources").EnumerateArray(),
            source => Assert.Equal("miss", source.GetProperty("cacheStatus").GetString()));
        Assert.Equal("valid", firstDocument.RootElement.GetProperty("quality").GetProperty("status").GetString());
        Assert.Equal(25, firstDocument.RootElement.GetProperty("hourly").GetArrayLength());
        Assert.Equal(
            0.8,
            firstDocument.RootElement
                .GetProperty("hourly")[0]
                .GetProperty("metrics")
                .GetProperty("waveHeightM")
                .GetDouble());
        Assert.Equal(
            "good",
            firstDocument.RootElement
                .GetProperty("hourly")[0]
                .GetProperty("overall")
                .GetProperty("riskLevel")
                .GetString());
        Assert.Equal(
            "template",
            firstDocument.RootElement.GetProperty("explanation").GetProperty("source").GetString());
        Assert.False(firstDocument.RootElement.GetProperty("explanation").GetProperty("degraded").GetBoolean());

        using var secondResponse = await client.PostAsJsonAsync("/api/v1/marine-analyses", request);
        using var secondDocument = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.All(
            secondDocument.RootElement.GetProperty("sources").EnumerateArray(),
            source => Assert.Equal("hit", source.GetProperty("cacheStatus").GetString()));

        using var conditionalRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/marine-analyses")
        {
            Content = JsonContent.Create(request)
        };
        conditionalRequest.Headers.TryAddWithoutValidation("If-None-Match", etag);
        using var conditionalResponse = await client.SendAsync(conditionalRequest);

        Assert.Equal(HttpStatusCode.NotModified, conditionalResponse.StatusCode);
        Assert.Equal(etag, conditionalResponse.Headers.GetValues("ETag").Single());
        Assert.Equal(1, factory.Weather.CallCount);
        Assert.Equal(1, factory.Marine.CallCount);
    }

    [Fact]
    public async Task AiExplanationReturnsSourceAiWhenProviderIsEnabled()
    {
        using var factory = new ApiTestApplicationFactory { EnableAiExplanation = true };
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/marine-analyses", CreateRequest());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ai", document.RootElement.GetProperty("explanation").GetProperty("source").GetString());
        Assert.False(document.RootElement.GetProperty("explanation").GetProperty("degraded").GetBoolean());
        Assert.Equal(1, factory.ExplanationProvider.CallCount);
    }

    [Fact]
    public async Task UnknownActivityReturnsValidationProblemDetails()
    {
        using var factory = new ApiTestApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/marine-analyses",
            new
            {
                location = new { latitude = 30.194, longitude = 122.687 },
                from = "2026-07-16T00:00:00Z",
                hours = 24,
                activities = UnsupportedActivities
            });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task InvalidHoursReturnsValidationProblemDetails()
    {
        using var factory = new ApiTestApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/marine-analyses",
            new
            {
                location = new { latitude = 30.194, longitude = 122.687 },
                from = "2026-07-16T00:00:00Z",
                hours = 48
            });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", document.RootElement.GetProperty("code").GetString());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("hours", out _));
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task LocationIdQueryResolvesCatalogMetadata()
    {
        using var factory = new ApiTestApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateDatabaseAsync();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/marine-analyses",
            new
            {
                location = new { locationId = "8a477d67-73fa-4f43-b954-cd29d238a89d" },
                from = "2026-07-16T00:00:00Z",
                hours = 24
            });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var location = document.RootElement.GetProperty("location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("东极岛", location.GetProperty("displayName").GetString());
        Assert.Equal("Asia/Shanghai", location.GetProperty("timeZone").GetString());
        Assert.Equal(
            "8a477d67-73fa-4f43-b954-cd29d238a89d",
            location.GetProperty("locationId").GetString());
        Assert.Equal(30.200, location.GetProperty("latitude").GetDouble());
    }

    [Fact]
    public async Task UnknownLocationIdReturnsLocationNotFoundProblemDetails()
    {
        using var factory = new ApiTestApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateDatabaseAsync();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/marine-analyses",
            new
            {
                location = new { locationId = "11111111-1111-1111-1111-111111111111" },
                from = "2026-07-16T00:00:00Z",
                hours = 24
            });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("LOCATION_NOT_FOUND", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task LocationIdAndCoordinatesCannotBeCombined()
    {
        using var factory = new ApiTestApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/marine-analyses",
            new
            {
                location = new
                {
                    locationId = "8a477d67-73fa-4f43-b954-cd29d238a89d",
                    latitude = 30.194,
                    longitude = 122.687
                },
                from = "2026-07-16T00:00:00Z",
                hours = 24
            });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ProviderFailureReturnsProblemDetailsWithTraceId()
    {
        using var factory = new ApiTestApplicationFactory();
        factory.Weather.ShouldFail = true;
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/marine-analyses",
            CreateRequest());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("PROVIDER_UNAVAILABLE", document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }

    private static object CreateRequest() => new
    {
        location = new { latitude = 30.194, longitude = 122.687 },
        from = "2026-07-16T00:00:00Z",
        hours = 24,
        activities = new[] { "boat" }
    };

    internal sealed class ApiTestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"marine-insight-analysis-{Guid.NewGuid():N}.db");

        public ApiTestApplicationFactory()
        {
            Weather = new FakeWeatherProvider();
            Marine = new FakeMarineProvider();
            ExplanationProvider = new FakeExplanationProvider();
        }

        public FakeWeatherProvider Weather { get; }

        public FakeMarineProvider Marine { get; }

        public FakeExplanationProvider ExplanationProvider { get; }

        public bool EnableAiExplanation { get; init; }

        public async Task MigrateDatabaseAsync()
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MarineInsightDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenTelemetry:Endpoint"] = "",
                    ["TideProviders:WorldTides:Enabled"] = "false",
                    ["AI:Enabled"] = "false",
                    ["Database:Provider"] = "Sqlite",
                    ["ConnectionStrings:MarineInsight"] = $"Data Source={_databasePath}",
                    ["Caching:Forecast:Environment"] = "api-test"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IWeatherForecastProvider>();
                services.RemoveAll<IMarineForecastProvider>();
                services.AddSingleton<IWeatherForecastProvider>(Weather);
                services.AddSingleton<IMarineForecastProvider>(Marine);
                if (EnableAiExplanation)
                {
                    services.RemoveAll<IExplanationProvider>();
                    services.AddSingleton<IExplanationProvider>(ExplanationProvider);
                }
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
    }

    public sealed class FakeWeatherProvider : IWeatherForecastProvider
    {
        public string ProviderCode => "api-weather";

        public ProviderIdentity Identity { get; } = new("api-weather", "configured-weather");

        public int CallCount { get; private set; }

        public bool ShouldFail { get; set; }

        public Task<ProviderForecastResult> GetWeatherAsync(
            GeoPoint location,
            ForecastRange range,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (ShouldFail)
            {
                return Task.FromException<ProviderForecastResult>(
                    new ProviderTimeoutException("api-weather", "test provider timeout"));
            }

            return Task.FromResult(new ProviderForecastResult(CreateBatch(
                ForecastDataDomain.Weather,
                Identity,
                location,
                range,
                ForecastMetricSet.Create(windSpeedMs: 4))));
        }
    }

    public sealed class FakeMarineProvider : IMarineForecastProvider
    {
        public string ProviderCode => "api-marine";

        public ProviderIdentity Identity { get; } = new("api-marine", "configured-marine");

        public int CallCount { get; private set; }

        public Task<ProviderForecastResult> GetMarineAsync(
            GeoPoint location,
            ForecastRange range,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new ProviderForecastResult(CreateBatch(
                ForecastDataDomain.Marine,
                Identity,
                location,
                range,
                ForecastMetricSet.Create(waveHeightM: 0.8))));
        }
    }

    public sealed class FakeExplanationProvider : IExplanationProvider
    {
        public string ProviderCode => "fake-ai";

        public string ModelVersion => "fake-model";

        public bool IsEnabled => true;

        public int CallCount { get; private set; }

        public Task<ExplanationCandidate> ExplainAsync(
            ExplanationFacts facts,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new ExplanationCandidate
            {
                Headline = "整体海况良好，适宜乘船活动。",
                Summary = "风浪较小，适合安排乘船活动。",
                ActivityNotes = [new ExplanationActivityNote { Activity = "boat", Text = "可以安排乘船活动。" }]
            });
        }
    }

    private static ForecastBatch CreateBatch(
        ForecastDataDomain dataDomain,
        ProviderIdentity provider,
        GeoPoint location,
        ForecastRange range,
        ForecastMetricSet metrics)
    {
        var batchId = Guid.NewGuid();
        var points = Enumerable.Range(0, range.Hours + 1)
            .Select(index =>
            {
                var time = range.StartUtc.AddHours(index);
                var quality = DataQuality.Valid();
                var sources = metrics.GetPresentMetrics()
                    .Select(metric => new MetricSource(
                        metric,
                        provider,
                        batchId,
                        time,
                        quality.Status,
                        quality.Freshness));
                return new ForecastPoint(time, metrics, quality, sources);
            })
            .ToArray();

        return new ForecastBatch(
            batchId,
            dataDomain,
            provider,
            location,
            null,
            range.StartUtc.AddHours(-1),
            range.StartUtc.AddHours(-1),
            range,
            points,
            DataQuality.Valid());
    }
}
