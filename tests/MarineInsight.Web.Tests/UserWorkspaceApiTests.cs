using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MarineInsight.Infrastructure.Persistence;
using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MarineInsight.Web.Tests;

public sealed class UserWorkspaceApiTests
{
    [Fact]
    public async Task UserCanCreateListUpdateAndDeleteOwnLocation()
    {
        using var factory = new WorkspaceApplicationFactory();
        await factory.MigrateDatabaseAsync();
        await factory.CreateUserAsync("owner@example.com", "Marine!Pass1");
        using var client = factory.CreateHttpsClient();
        await WorkspaceApplicationFactory.LoginAsync(client, "owner@example.com", "Marine!Pass1");
        var token = await WorkspaceApplicationFactory.GetAuthenticatedAntiforgeryTokenAsync(client);

        using var createResponse = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/user-locations",
            new { name = "TestPoint", latitude = 30.72, longitude = 122.77, defaultActivity = "boat", note = "demo", sortOrder = 1 },
            token);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync()).RootElement;
        var id = Guid.Parse(created.GetProperty("id").GetString()!);

        using var listResponse = await client.GetAsync("/api/v1/user-locations");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("TestPoint", list[0].GetProperty("name").GetString());

        using var updateResponse = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/user-locations/{id}",
            new { name = "TestPointRenamed", latitude = 30.72, longitude = 122.77, defaultActivity = "camping", note = "demo", sortOrder = 2 },
            token);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var deleteResponse = await SendJsonAsync(client, HttpMethod.Delete, $"/api/v1/user-locations/{id}", null, token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var emptyResponse = await client.GetAsync("/api/v1/user-locations");
        var emptyList = JsonDocument.Parse(await emptyResponse.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(0, emptyList.GetArrayLength());
    }

    [Fact]
    public async Task UserCannotSeeOrDeleteAnotherUsersLocation()
    {
        using var factory = new WorkspaceApplicationFactory();
        await factory.MigrateDatabaseAsync();
        await factory.CreateUserAsync("owner@example.com", "Marine!Pass1");
        await factory.CreateUserAsync("intruder@example.com", "Marine!Pass1");

        using var ownerClient = factory.CreateHttpsClient();
        await WorkspaceApplicationFactory.LoginAsync(ownerClient, "owner@example.com", "Marine!Pass1");
        var ownerToken = await WorkspaceApplicationFactory.GetAuthenticatedAntiforgeryTokenAsync(ownerClient);
        using var createResponse = await SendJsonAsync(
            ownerClient,
            HttpMethod.Post,
            "/api/v1/user-locations",
            new { name = "Private", latitude = 30.0, longitude = 122.0, sortOrder = 0 },
            ownerToken);
        var id = Guid.Parse(JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!);

        using var intruderClient = factory.CreateHttpsClient();
        await WorkspaceApplicationFactory.LoginAsync(intruderClient, "intruder@example.com", "Marine!Pass1");
        var intruderToken = await WorkspaceApplicationFactory.GetAuthenticatedAntiforgeryTokenAsync(intruderClient);

        using var listResponse = await intruderClient.GetAsync("/api/v1/user-locations");
        var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(0, list.GetArrayLength());

        using var deleteResponse = await SendJsonAsync(intruderClient, HttpMethod.Delete, $"/api/v1/user-locations/{id}", null, intruderToken);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task UserCanDeleteAndClearOwnQueryHistory()
    {
        using var factory = new WorkspaceApplicationFactory();
        await factory.MigrateDatabaseAsync();
        var userId = await factory.CreateUserAsync("historian@example.com", "Marine!Pass1");
        await factory.SeedHistoryAsync(userId, 2);
        using var client = factory.CreateHttpsClient();
        await WorkspaceApplicationFactory.LoginAsync(client, "historian@example.com", "Marine!Pass1");
        var token = await WorkspaceApplicationFactory.GetAuthenticatedAntiforgeryTokenAsync(client);

        using var listResponse = await client.GetAsync("/api/v1/query-history");
        var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(2, list.GetArrayLength());
        var firstId = Guid.Parse(list[0].GetProperty("id").GetString()!);

        using var deleteResponse = await SendJsonAsync(client, HttpMethod.Delete, $"/api/v1/query-history/{firstId}", null, token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var afterDelete = await client.GetAsync("/api/v1/query-history");
        Assert.Equal(1, JsonDocument.Parse(await afterDelete.Content.ReadAsStringAsync()).RootElement.GetArrayLength());

        using var clearResponse = await SendJsonAsync(client, HttpMethod.Delete, "/api/v1/query-history", null, token);
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);

        using var emptyResponse = await client.GetAsync("/api/v1/query-history");
        Assert.Equal(0, JsonDocument.Parse(await emptyResponse.Content.ReadAsStringAsync()).RootElement.GetArrayLength());
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        object? body,
        string? antiforgeryToken)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (antiforgeryToken is not null)
        {
            request.Headers.TryAddWithoutValidation("RequestVerificationToken", antiforgeryToken);
        }

        return await client.SendAsync(request);
    }

    private sealed class WorkspaceApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"marine-insight-workspace-{Guid.NewGuid():N}.db");

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

        public async Task SeedHistoryAsync(Guid userId, int count)
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MarineInsightDbContext>();
            for (var i = 0; i < count; i++)
            {
                dbContext.QueryHistory.Add(new QueryHistoryEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    DisplayName = $"测试点{i}",
                    Latitude = 30.2 + i * 0.1,
                    Longitude = 122.6,
                    ForecastFromUtc = new DateTimeOffset(2026, 8, 14, i, 0, 0, TimeSpan.Zero),
                    Hours = 24,
                    Activities = "Boat",
                    AnalysisId = Guid.NewGuid(),
                    RiskLevel = "good",
                    Score = 72,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
            }

            await dbContext.SaveChangesAsync();
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

        public static async Task<string> GetAuthenticatedAntiforgeryTokenAsync(HttpClient client)
        {
            using var response = await client.GetAsync("/");
            response.EnsureSuccessStatusCode();
            return ExtractAntiforgeryToken(await response.Content.ReadAsStringAsync());
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
                    ["Identity:RequireConfirmedEmail"] = "false",
                    ["Captcha:Enabled"] = "false",
                    ["AI:Enabled"] = "false"
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
