using System.Net;
using System.Text.RegularExpressions;
using MarineInsight.Infrastructure.Persistence;
using MarineInsight.Web.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MarineInsight.Web.Tests;

public sealed class CaptchaTests
{
    [Fact]
    public async Task RegisterPageRendersCaptchaWhenEnabled()
    {
        using var factory = new CaptchaApplicationFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/account/register");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"CaptchaId\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"CaptchaCode\"", html, StringComparison.Ordinal);
        Assert.Contains("<svg", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrongCaptchaRedirectsToCaptchaError()
    {
        using var factory = new CaptchaApplicationFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateHttpsClient();
        var token = await GetAntiforgeryTokenAsync(client, "/account/register");

        using var response = await client.PostAsync(
            "/account/register",
            Form(
                ("Email", "captcha@example.com"),
                ("Password", "Marine!Pass1"),
                ("ConfirmPassword", "Marine!Pass1"),
                ("CaptchaId", Guid.NewGuid().ToString("N")),
                ("CaptchaCode", "ZZZZ"),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/register?error=captcha", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task CorrectCaptchaAllowsRegistration()
    {
        using var factory = new CaptchaApplicationFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateHttpsClient();

        var challenge = factory.GenerateCaptcha();
        var token = await GetAntiforgeryTokenAsync(client, "/account/register");

        using var response = await client.PostAsync(
            "/account/register",
            Form(
                ("Email", "captcha-ok@example.com"),
                ("Password", "Marine!Pass1"),
                ("ConfirmPassword", "Marine!Pass1"),
                ("CaptchaId", challenge.Id),
                ("CaptchaCode", challenge.Code),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task CaptchaCodeIsCaseInsensitive()
    {
        using var factory = new CaptchaApplicationFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateHttpsClient();

        var challenge = factory.GenerateCaptcha();
        var token = await GetAntiforgeryTokenAsync(client, "/account/register");

        using var response = await client.PostAsync(
            "/account/register",
            Form(
                ("Email", "captcha-lower@example.com"),
                ("Password", "Marine!Pass1"),
                ("ConfirmPassword", "Marine!Pass1"),
                ("CaptchaId", challenge.Id),
                ("CaptchaCode", challenge.Code.ToLowerInvariant()),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task DuplicateEmailRedirectsToEmailExistsError()
    {
        using var factory = new CaptchaApplicationFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateHttpsClient();

        var first = factory.GenerateCaptcha();
        var firstToken = await GetAntiforgeryTokenAsync(client, "/account/register");
        using var create = await client.PostAsync(
            "/account/register",
            Form(
                ("Email", "dup@example.com"),
                ("Password", "Marine!Pass1"),
                ("ConfirmPassword", "Marine!Pass1"),
                ("CaptchaId", first.Id),
                ("CaptchaCode", first.Code),
                ("__RequestVerificationToken", firstToken)));
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        Assert.Equal("/", create.Headers.Location?.OriginalString);

        var second = factory.GenerateCaptcha();
        var secondToken = await GetAntiforgeryTokenAsync(client, "/account/register");
        using var duplicate = await client.PostAsync(
            "/account/register",
            Form(
                ("Email", "dup@example.com"),
                ("Password", "Marine!Pass1"),
                ("ConfirmPassword", "Marine!Pass1"),
                ("CaptchaId", second.Id),
                ("CaptchaCode", second.Code),
                ("__RequestVerificationToken", secondToken)));

        Assert.Equal(HttpStatusCode.Redirect, duplicate.StatusCode);
        Assert.Equal("/account/register?error=email-exists", duplicate.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task CaptchaIsSingleUse()
    {
        using var factory = new CaptchaApplicationFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateHttpsClient();

        var challenge = factory.GenerateCaptcha();
        var token = await GetAntiforgeryTokenAsync(client, "/account/login");

        // First attempt consumes the challenge but fails on an unknown account.
        using var first = await client.PostAsync(
            "/account/login",
            Form(
                ("Email", "nobody@example.com"),
                ("Password", "Wrong!Pass1"),
                ("CaptchaId", challenge.Id),
                ("CaptchaCode", challenge.Code),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal("/account/login?error=invalid", first.Headers.Location?.OriginalString);

        // Reusing the same challenge now fails the captcha check before credentials are tried.
        using var second = await client.PostAsync(
            "/account/login",
            Form(
                ("Email", "nobody@example.com"),
                ("Password", "Wrong!Pass1"),
                ("CaptchaId", challenge.Id),
                ("CaptchaCode", challenge.Code),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Equal("/account/login?error=captcha", second.Headers.Location?.OriginalString);
    }

    private sealed class CaptchaApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"marine-insight-captcha-{Guid.NewGuid():N}.db");

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

        public CaptchaChallenge GenerateCaptcha()
        {
            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<CaptchaService>().Generate();
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
                    ["Captcha:Enabled"] = "true"
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
