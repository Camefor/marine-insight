using System.Net;
using System.Threading.RateLimiting;
using MarineInsight.Application.Analysis;
using MarineInsight.Application.Forecast;
using MarineInsight.Application.Locations;
using MarineInsight.Application.Users;
using MarineInsight.Domain.Analysis;
using MarineInsight.Infrastructure.Caching;
using MarineInsight.Infrastructure.Persistence;
using MarineInsight.Infrastructure.Providers.Explanation;
using MarineInsight.Infrastructure.Providers.OpenMeteo;
using MarineInsight.Infrastructure.Providers.WorldTides;
using MarineInsight.Web.Api;
using MarineInsight.Web.Authentication;
using MarineInsight.Web.Components;
using MarineInsight.Web.Components.Features.Dashboard;
using MarineInsight.Web.Health;
using MarineInsight.Web.Observability;
using MarineInsight.Web.Operations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

// Container secrets use configuration-key filenames such as ConnectionStrings__MarineInsight.
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.With(new UtcTimestampEnricher())
        .Enrich.With(new ActivityEnricher())
        .Enrich.With(new SensitiveDataEnricher())
        .Enrich.WithProperty("service", MarineInsightTelemetry.ServiceName)
        .Enrich.WithProperty("environment", context.HostingEnvironment.EnvironmentName)
        .Enrich.WithProperty(
            "version",
            typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0")
        .WriteTo.Console(new JsonFormatter());
});

// Keep provider selection in configuration so local SQLite and production PostgreSQL use the same boundary.
builder.Services.AddMarineInsightPersistence(builder.Configuration);
builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-MarineInsight.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";
});
builder.Services.AddAuthorization(options =>
    options.AddPolicy("Administrator", policy => policy.RequireRole("Administrator")));
if (builder.Configuration["DataProtection:KeyPath"] is { Length: > 0 } keyPath)
{
    // Authentication cookies must survive container replacement; production mounts this path
    // on a protected persistent volume shared by all Web replicas.
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
        .SetApplicationName("MarineInsight");
}
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var value in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    {
        if (IPAddress.TryParse(value, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("account", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddMarineInsightCaching(builder.Configuration);
builder.Services.AddOpenMeteoForecastProviders(builder.Configuration);
builder.Services.AddWorldTidesProvider(builder.Configuration);
builder.Services.AddExplanationProvider(builder.Configuration);
builder.Services.AddSingleton<ForecastSnapshotAssembler>();
builder.Services.AddSingleton<MarineRiskRuleEngine>();
builder.Services.AddScoped<MarineAnalysisQueryService>();
builder.Services.AddScoped<ExplanationService>();
builder.Services.AddScoped<LocationQueryService>();
builder.Services.AddScoped<UserWorkspaceService>();
builder.Services.AddScoped<OperationsOverviewService>();
builder.Services.AddScoped<DashboardQuerySession>();
builder.Services.AddMarineInsightTelemetry(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddCheck<MarineInsightDatabaseHealthCheck>(
        "database",
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(3));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<MarineInsightDbContext>();
    await database.Database.MigrateAsync();
    return;
}

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = (_, _, exception) => exception is null
        ? LogEventLevel.Information
        : LogEventLevel.Error;
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("eventId", "HTTP_REQUEST_COMPLETED");
        diagnosticContext.Set("endpoint", httpContext.GetEndpoint()?.DisplayName);
    };
});
// 健康检查的非 2xx 是探针结果，不能被页面错误重执行中间件改写。
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/health"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
// 容器探针直接访问 HTTP，健康端点不应被强制 HTTPS 重定向。
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/health"),
    branch => branch.UseHttpsRedirection());

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready", StringComparer.OrdinalIgnoreCase),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapLocationEndpoints();
app.MapMarineAnalysisEndpoints();
app.MapAccountEndpoints();
app.MapUserWorkspaceEndpoints();
app.MapOperationsEndpoints();

app.Run();

public partial class Program;
