using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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

public sealed class AdminApiTests
{
    private const string AdminEmail = "xuehaq@gmail.com";
    private const string Password = "Marine!Pass1";

    [Fact]
    public async Task RegisteringAdminEmailGrantsRoleAndCanWriteLocations()
    {
        using var factory = new AdminApplicationFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateHttpsClient();
        await AdminApplicationFactory.RegisterAsync(client, AdminEmail, Password);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<MarineInsightUser>>();
        var admin = await userManager.FindByEmailAsync(AdminEmail);
        Assert.NotNull(admin);
        Assert.True(await userManager.IsInRoleAsync(admin, "Administrator"));

        var token = await AdminApplicationFactory.GetAuthenticatedAntiforgeryTokenAsync(client);
        using var createResponse = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/admin/locations",
            new { displayName = "枸杞岛", latitude = 30.72, longitude = 122.77, timeZoneId = "Asia/Shanghai", locationType = "island", coastOrientationDeg = 45 },
            token);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var listResponse = await client.GetAsync("/api/v1/admin/locations");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync()).RootElement;
        Assert.Contains(list.EnumerateArray(), item => item.GetProperty("displayName").GetString() == "枸杞岛");
    }

    [Fact]
    public async Task NonAdministratorCannotAccessAdminEndpoints()
    {
        using var factory = new AdminApplicationFactory();
        await factory.MigrateDatabaseAsync();
        await factory.CreateUserAsync("member@example.com", Password);
        using var client = factory.CreateHttpsClient();
        await AdminApplicationFactory.LoginAsync(client, "member@example.com", Password);

        // Cookie 认证在授权失败时按 AccessDeniedPath 重定向（Program.cs），而非直接返回 403。
        using var locationsResponse = await client.GetAsync("/api/v1/admin/locations");
        using var usersResponse = await client.GetAsync("/api/v1/admin/users");

        Assert.Equal(HttpStatusCode.Redirect, locationsResponse.StatusCode);
        Assert.Equal("/account/access-denied", new Uri(locationsResponse.Headers.Location!.OriginalString).AbsolutePath);
        Assert.Equal(HttpStatusCode.Redirect, usersResponse.StatusCode);
        Assert.Equal("/account/access-denied", new Uri(usersResponse.Headers.Location!.OriginalString).AbsolutePath);
    }

    [Fact]
    public async Task AdminCanCreateUpdateAndDeletePresetLocation()
    {
        using var factory = new AdminApplicationFactory();
        await factory.MigrateDatabaseAsync();
        using var adminClient = factory.CreateHttpsClient();
        await AdminApplicationFactory.RegisterAsync(adminClient, AdminEmail, Password);
        var token = await AdminApplicationFactory.GetAuthenticatedAntiforgeryTokenAsync(adminClient);

        using var createResponse = await SendJsonAsync(
            adminClient,
            HttpMethod.Post,
            "/api/v1/admin/locations",
            new { displayName = "测试港口", latitude = 31.5, longitude = 122.3, timeZoneId = "Asia/Shanghai", locationType = "port", coastOrientationDeg = (double?)null },
            token);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync()).RootElement;
        var id = Guid.Parse(created.GetProperty("id").GetString()!);

        using var listResponse = await adminClient.GetAsync("/api/v1/admin/locations");
        var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync()).RootElement;
        Assert.Contains(list.EnumerateArray(), item => item.GetProperty("id").GetString() == id.ToString());

        using var updateResponse = await SendJsonAsync(
            adminClient,
            HttpMethod.Put,
            $"/api/v1/admin/locations/{id}",
            new { displayName = "测试港口-改名", latitude = 31.5, longitude = 122.3, timeZoneId = "Asia/Shanghai", locationType = "port", coastOrientationDeg = (double?)null },
            token);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var deleteResponse = await SendJsonAsync(adminClient, HttpMethod.Delete, $"/api/v1/admin/locations/{id}", null, token);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var deleteResult = JsonDocument.Parse(await deleteResponse.Content.ReadAsStringAsync()).RootElement;
        Assert.True(deleteResult.GetProperty("deleted").GetBoolean());
        Assert.Equal(0, deleteResult.GetProperty("cascadedFavoriteCount").GetInt32());

        using var emptyResponse = await adminClient.GetAsync("/api/v1/admin/locations");
        var afterList = JsonDocument.Parse(await emptyResponse.Content.ReadAsStringAsync()).RootElement;
        Assert.DoesNotContain(afterList.EnumerateArray(), item => item.GetProperty("id").GetString() == id.ToString());
    }

    [Fact]
    public async Task AdminCannotCreateDuplicateNameCoordinate()
    {
        using var factory = new AdminApplicationFactory();
        await factory.MigrateDatabaseAsync();
        using var adminClient = factory.CreateHttpsClient();
        await AdminApplicationFactory.RegisterAsync(adminClient, AdminEmail, Password);
        var token = await AdminApplicationFactory.GetAuthenticatedAntiforgeryTokenAsync(adminClient);

        var payload = new { displayName = "枸杞岛", latitude = 30.72, longitude = 122.77, timeZoneId = "Asia/Shanghai", locationType = "island", coastOrientationDeg = (double?)null };
        using var first = await SendJsonAsync(adminClient, HttpMethod.Post, "/api/v1/admin/locations", payload, token);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var duplicate = await SendJsonAsync(adminClient, HttpMethod.Post, "/api/v1/admin/locations", payload, token);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var problem = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("LOCATION_CONFLICT", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AdminCanListRegisteredUsersReadOnly()
    {
        using var factory = new AdminApplicationFactory();
        await factory.MigrateDatabaseAsync();
        await factory.CreateUserAsync("member@example.com", Password);
        using var adminClient = factory.CreateHttpsClient();
        await AdminApplicationFactory.RegisterAsync(adminClient, AdminEmail, Password);

        using var usersResponse = await adminClient.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
        var users = JsonDocument.Parse(await usersResponse.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(2, users.GetArrayLength());
        Assert.Equal("member@example.com", users[0].GetProperty("email").GetString());
        Assert.Equal(AdminEmail, users[1].GetProperty("email").GetString());
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

    private sealed class AdminApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"marine-insight-admin-{Guid.NewGuid():N}.db");

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

        public static async Task RegisterAsync(HttpClient client, string email, string password)
        {
            var token = await GetAntiforgeryTokenAsync(client, "/account/register");
            using var response = await client.PostAsync(
                "/account/register",
                Form(
                    ("Email", email),
                    ("Password", password),
                    ("ConfirmPassword", password),
                    ("ReturnUrl", "/"),
                    ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
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
                    ["AI:Enabled"] = "false",
                    ["Admin:Email"] = AdminEmail
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
