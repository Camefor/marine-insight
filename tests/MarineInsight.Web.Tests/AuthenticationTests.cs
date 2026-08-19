using System.Net;
using System.Net.Http.Headers;
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

public sealed class AuthenticationTests
{
    [Fact]
    public async Task AnonymousDashboardAndAccountPagesRemainAvailable()
    {
        using var factory = new AuthenticationApplicationFactory();
        using var client = factory.CreateHttpsClient();

        using var dashboardResponse = await client.GetAsync("/");
        using var loginResponse = await client.GetAsync("/account/login");
        var loginHtml = await loginResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Contains("登录", loginHtml, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(ExtractAntiforgeryToken(loginHtml)));

        using var errorResponse = await client.GetAsync("/account/login?error=locked");
        Assert.Equal(HttpStatusCode.OK, errorResponse.StatusCode);
        Assert.Contains(
            "登录尝试过多",
            await errorResponse.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterCreatesUserAndSecureAuthenticationCookie()
    {
        using var factory = new AuthenticationApplicationFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateHttpsClient();
        var token = await GetAntiforgeryTokenAsync(client, "/account/register");

        using var response = await client.PostAsync(
            "/account/register",
            Form(
                ("Email", "skipper@example.com"),
                ("Password", "Marine!Pass1"),
                ("ConfirmPassword", "Marine!Pass1"),
                ("ReturnUrl", "https://attacker.example/redirect"),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        var cookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-MarineInsight.Auth=", StringComparison.Ordinal));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<MarineInsightUser>>();
        var user = await userManager.FindByEmailAsync("skipper@example.com");
        Assert.NotNull(user);
        Assert.NotEqual("Marine!Pass1", user.PasswordHash);
    }

    [Fact]
    public async Task LoginFailuresLockAccountAfterConfiguredThreshold()
    {
        using var factory = new AuthenticationApplicationFactory();
        await factory.MigrateDatabaseAsync();
        await factory.CreateUserAsync("locked@example.com", "Marine!Pass1");
        using var client = factory.CreateHttpsClient();
        var token = await GetAntiforgeryTokenAsync(client, "/account/login");

        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            response?.Dispose();
            response = await client.PostAsync(
                "/account/login",
                Form(
                    ("Email", "locked@example.com"),
                    ("Password", "Wrong!Pass1"),
                    ("__RequestVerificationToken", token)));
        }

        using (response)
        {
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/account/login?error=locked", response.Headers.Location?.OriginalString);
        }

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<MarineInsightUser>>();
        var user = await userManager.FindByEmailAsync("locked@example.com");
        Assert.NotNull(user?.LockoutEnd);
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ExistingUserCanLoginAndLogoutWithAntiforgeryProtection()
    {
        using var factory = new AuthenticationApplicationFactory();
        await factory.MigrateDatabaseAsync();
        await factory.CreateUserAsync("member@example.com", "Marine!Pass1");
        using var client = factory.CreateHttpsClient();
        var loginToken = await GetAntiforgeryTokenAsync(client, "/account/login");

        using var loginResponse = await client.PostAsync(
            "/account/login",
            Form(
                ("Email", "member@example.com"),
                ("Password", "Marine!Pass1"),
                ("ReturnUrl", "/home"),
                ("__RequestVerificationToken", loginToken)));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal("/home", loginResponse.Headers.Location?.OriginalString);
        Assert.Contains(
            loginResponse.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-MarineInsight.Auth=", StringComparison.Ordinal));

        using var dashboardResponse = await client.GetAsync("/");
        var dashboardHtml = await dashboardResponse.Content.ReadAsStringAsync();
        Assert.Contains("member@example.com", dashboardHtml, StringComparison.Ordinal);
        var logoutToken = ExtractAntiforgeryToken(dashboardHtml);

        using var logoutResponse = await client.PostAsync(
            "/account/logout",
            Form(("__RequestVerificationToken", logoutToken)));

        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
        Assert.Equal("/", logoutResponse.Headers.Location?.OriginalString);
        Assert.Contains(
            logoutResponse.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-MarineInsight.Auth=", StringComparison.Ordinal)
                && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AccountWriteWithoutAntiforgeryTokenIsRejected()
    {
        using var factory = new AuthenticationApplicationFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsync(
            "/account/register",
            Form(("Email", "missing-token@example.com"), ("Password", "Marine!Pass1")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private sealed class AuthenticationApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly bool _enableCaptcha;
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"marine-insight-auth-{Guid.NewGuid():N}.db");

        public AuthenticationApplicationFactory(bool enableCaptcha = false)
        {
            _enableCaptcha = enableCaptcha;
        }

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

        public async Task CreateUserAsync(string email, string password)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<MarineInsightUser>>();
            var result = await userManager.CreateAsync(
                new MarineInsightUser { Id = Guid.NewGuid(), UserName = email, Email = email },
                password);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
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
                    ["Captcha:Enabled"] = _enableCaptcha ? "true" : "false",
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
    }
}
