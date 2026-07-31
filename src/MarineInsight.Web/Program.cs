using MarineInsight.Application.Analysis;
using MarineInsight.Application.Forecast;
using MarineInsight.Application.Locations;
using MarineInsight.Domain.Analysis;
using MarineInsight.Infrastructure.Caching;
using MarineInsight.Infrastructure.Persistence;
using MarineInsight.Infrastructure.Providers.OpenMeteo;
using MarineInsight.Web.Api;
using MarineInsight.Web.Components;
using MarineInsight.Web.Components.Features.Dashboard;
using MarineInsight.Web.Health;
using MarineInsight.Web.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddMarineInsightCaching(builder.Configuration);
builder.Services.AddOpenMeteoForecastProviders(builder.Configuration);
builder.Services.AddSingleton<ForecastSnapshotAssembler>();
builder.Services.AddSingleton<MarineRiskRuleEngine>();
builder.Services.AddScoped<MarineAnalysisQueryService>();
builder.Services.AddScoped<LocationQueryService>();
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

// Configure the HTTP request pipeline.
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

app.Run();

public partial class Program;
