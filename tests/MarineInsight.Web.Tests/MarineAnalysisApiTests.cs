using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarineInsight.Application.Errors;
using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MarineInsight.Web.Tests;

public sealed class MarineAnalysisApiTests
{
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
        Assert.Equal("metricsOnly", firstDocument.RootElement.GetProperty("analysisStatus").GetString());
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

        using var secondResponse = await client.PostAsJsonAsync("/api/v1/marine-analyses", request);
        using var secondDocument = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.All(
            secondDocument.RootElement.GetProperty("sources").EnumerateArray(),
            source => Assert.Equal("hit", source.GetProperty("cacheStatus").GetString()));
        Assert.Equal(1, factory.Weather.CallCount);
        Assert.Equal(1, factory.Marine.CallCount);
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
        Assert.Equal(30.194, location.GetProperty("latitude").GetDouble());
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
        }

        public FakeWeatherProvider Weather { get; }

        public FakeMarineProvider Marine { get; }

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
