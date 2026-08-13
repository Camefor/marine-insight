using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MarineInsight.Application.Analysis.Ports;
using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MarineInsight.Web.Tests;

public sealed class AnalysisReportApiTests
{
    [Fact]
    public async Task AuthenticatedUserReadsOwnReport()
    {
        using var factory = new ReportApplicationFactory();
        await factory.MigrateDatabaseAsync();
        var ownerId = await factory.CreateUserAsync("owner@example.com", "Marine!Pass1");
        var report = await factory.SaveReportAsync(ownerId, RiskLevel.Caution);
        using var client = factory.CreateHttpsClient();
        await ReportApplicationFactory.LoginAsync(client, "owner@example.com", "Marine!Pass1");

        using var response = await client.GetAsync($"/api/v1/marine-analyses/{report.Id}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(report.Id.ToString(), document.RootElement.GetProperty("id").GetString());
        Assert.Equal(
            "caution",
            document.RootElement.GetProperty("overall").GetProperty("riskLevel").GetString());
        Assert.Equal(
            72,
            document.RootElement.GetProperty("overall").GetProperty("score").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("sources").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("risks").GetArrayLength());
    }

    [Fact]
    public async Task UserCannotReadAnotherUsersReport()
    {
        using var factory = new ReportApplicationFactory();
        await factory.MigrateDatabaseAsync();
        var ownerId = await factory.CreateUserAsync("owner@example.com", "Marine!Pass1");
        await factory.CreateUserAsync("intruder@example.com", "Marine!Pass1");
        var report = await factory.SaveReportAsync(ownerId, RiskLevel.Avoid);
        using var client = factory.CreateHttpsClient();
        await ReportApplicationFactory.LoginAsync(client, "intruder@example.com", "Marine!Pass1");

        using var response = await client.GetAsync($"/api/v1/marine-analyses/{report.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MissingReportReturnsAnalysisNotFound()
    {
        using var factory = new ReportApplicationFactory();
        await factory.MigrateDatabaseAsync();
        await factory.CreateUserAsync("owner@example.com", "Marine!Pass1");
        using var client = factory.CreateHttpsClient();
        await ReportApplicationFactory.LoginAsync(client, "owner@example.com", "Marine!Pass1");

        using var response = await client.GetAsync($"/api/v1/marine-analyses/{Guid.NewGuid()}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ANALYSIS_NOT_FOUND", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AnonymousRequestIsRedirectedToLogin()
    {
        using var factory = new ReportApplicationFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync($"/api/v1/marine-analyses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/account/login",
            response.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ReportApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"marine-insight-report-{Guid.NewGuid():N}.db");

        public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        public async Task MigrateDatabaseAsync()
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MarineInsightDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        public async Task<Guid> CreateUserAsync(string email, string password)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<MarineInsightUser>>();
            var user = new MarineInsightUser { Id = Guid.NewGuid(), UserName = email, Email = email };
            var result = await userManager.CreateAsync(user, password);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
            return user.Id;
        }

        public static async Task LoginAsync(HttpClient client, string email, string password)
        {
            var token = await GetAntiforgeryTokenAsync(client, "/account/login");
            using var response = await client.PostAsync(
                "/account/login",
                Form(
                    ("Email", email),
                    ("Password", password),
                    ("ReturnUrl", "/"),
                    ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        public async Task<AnalysisReport> SaveReportAsync(Guid userId, RiskLevel riskLevel)
        {
            using var scope = Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAnalysisReportRepository>();
            var report = CreateReport(userId, riskLevel);
            await repository.SaveAsync(report);
            return report;
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
                    ["Database:Provider"] = "Sqlite",
                    ["ConnectionStrings:MarineInsight"] = $"Data Source={_databasePath}",
                    ["Identity:RequireConfirmedEmail"] = "false"
                });
            });
            builder.ConfigureServices(services =>
            {
                // Force a per-test database even if host configuration ordering changes.
                services.RemoveAll<MarineInsightDbContext>();
                services.RemoveAll<DbContextOptions<MarineInsightDbContext>>();
                services.AddDbContext<MarineInsightDbContext>(options =>
                    options.UseSqlite($"Data Source={_databasePath};Pooling=False"));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            SqliteConnection.ClearAllPools();
            if (disposing && File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }

        private static AnalysisReport CreateReport(Guid userId, RiskLevel riskLevel) => new(
            Guid.NewGuid(),
            userId,
            null,
            new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero),
            24,
            "marine-score-1.0.0",
            "abc123",
            ActivityType.Boat,
            72,
            riskLevel,
            0.8,
            new DateTimeOffset(2026, 7, 16, 3, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 16, 7, 0, 0, TimeSpan.Zero),
            "rule-template.v1",
            DateTimeOffset.UtcNow,
            [
                new AnalysisRisk(
                    new DateTimeOffset(2026, 7, 16, 2, 0, 0, TimeSpan.Zero),
                    "swell-high",
                    RiskSeverity.Warning,
                    2.5,
                    2.0,
                    15,
                    "长周期涌浪偏高")
            ],
            [
                new AnalysisSourceBatch(
                    Guid.NewGuid(),
                    ForecastDataDomain.Weather,
                    "open-meteo",
                    "weather-v1",
                    AnalysisSourceRole.Primary,
                    "forecast-snapshot-assembler.v1")
            ]);

        private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
        {
            using var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            return ExtractAntiforgeryToken(await response.Content.ReadAsStringAsync());
        }

        private static string ExtractAntiforgeryToken(string html)
        {
            var match = Regex.Match(
                html,
                "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
                RegexOptions.CultureInvariant);
            return WebUtility.HtmlDecode(match.Groups["token"].Value);
        }

        private static FormUrlEncodedContent Form(params (string Name, string Value)[] values) =>
            new(values.Select(value => new KeyValuePair<string, string>(value.Name, value.Value)));
    }
}
