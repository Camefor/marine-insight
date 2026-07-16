using System.Diagnostics;
using System.Net;
using System.Text.Json;
using MarineInsight.Web.Observability;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MarineInsight.Web.Tests;

public sealed class HealthAndObservabilityTests
{
    [Fact]
    public async Task LiveHealthEndpointDoesNotRequireDatabase()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("healthy", document.RootElement.GetProperty("status").GetString());
        Assert.Empty(document.RootElement.GetProperty("checks").EnumerateArray());
    }

    [Fact]
    public async Task ReadyHealthEndpointReportsDatabaseStatus()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var checks = document.RootElement.GetProperty("checks").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            checks,
            check => check.GetProperty("name").GetString() == "database"
                && check.GetProperty("status").GetString() == "healthy");
    }

    [Fact]
    public void OtlpEndpointMustBeAnHttpUri()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:Endpoint"] = "http://collector:4317"
            })
            .Build();

        var endpoint = MarineInsightTelemetry.TryGetOtlpEndpoint(configuration);

        Assert.Equal("http://collector:4317/", endpoint?.ToString());
        Assert.Null(MarineInsightTelemetry.TryGetOtlpEndpoint(new ConfigurationBuilder().Build()));
        Assert.Null(MarineInsightTelemetry.TryGetOtlpEndpoint(BuildConfiguration("file://collector")));

        var services = new ServiceCollection();
        services.AddMarineInsightTelemetry(BuildConfiguration(string.Empty));
        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetService<TracerProvider>());
        Assert.Equal(ActivityIdFormat.W3C, Activity.DefaultIdFormat);
    }

    [Fact]
    public void ActivityEnricherAddsW3CTraceFields()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MarineInsightTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .Enrich.With<ActivityEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();
        using var activity = MarineInsightTelemetry.ActivitySource.StartActivity("test-span");

        logger.Information("trace test");

        Assert.NotNull(sink.Event);
        Assert.Equal(activity?.TraceId.ToHexString(), GetScalar(sink.Event!, "traceId"));
        Assert.Equal(activity?.SpanId.ToHexString(), GetScalar(sink.Event!, "spanId"));
    }

    [Fact]
    public void SensitiveDataEnricherRedactsSecretsAndPreciseLocation()
    {
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .Enrich.With<SensitiveDataEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information(
            "provider request {ApiKey} {Authorization} {Latitude} {Payload} {LocationGrid}",
            "secret-api-key",
            "Bearer secret-token",
            31.2345,
            "password=hidden; token=also-hidden",
            "31.2,121.5");

        Assert.NotNull(sink.Event);
        Assert.Equal(SensitiveDataEnricher.RedactedValue, GetScalar(sink.Event!, "ApiKey"));
        Assert.Equal(SensitiveDataEnricher.RedactedValue, GetScalar(sink.Event!, "Authorization"));
        Assert.Equal(SensitiveDataEnricher.RedactedValue, GetScalar(sink.Event!, "Latitude"));
        Assert.Equal("password=[REDACTED]; token=[REDACTED]", GetScalar(sink.Event!, "Payload"));
        Assert.Equal("31.2,121.5", GetScalar(sink.Event!, "LocationGrid"));
    }

    [Fact]
    public void UtcTimestampEnricherWritesUtcValue()
    {
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .Enrich.With<UtcTimestampEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("timestamp test");

        var timestamp = Assert.IsType<DateTime>(GetScalar(sink.Event!, "timestamp"));
        Assert.Equal(DateTimeKind.Utc, timestamp.Kind);
    }

    private static IConfiguration BuildConfiguration(string endpoint)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:Endpoint"] = endpoint
            })
            .Build();
    }

    private static object? GetScalar(LogEvent logEvent, string propertyName)
    {
        var value = logEvent.Properties[propertyName];
        Assert.IsType<ScalarValue>(value);
        return ((ScalarValue)value).Value;
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public LogEvent? Event { get; private set; }

        public void Emit(LogEvent logEvent)
        {
            Event = logEvent;
        }
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath;

        public TestApplicationFactory()
        {
            var fileName = $"marine-insight-health-{Guid.NewGuid():N}.db";
            _databasePath = Path.Combine(Path.GetTempPath(), fileName);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["OpenTelemetry:Endpoint"] = ""
                };

                values["Database:Provider"] = "Sqlite";
                values["ConnectionStrings:MarineInsight"] = $"Data Source={_databasePath}";

                configuration.AddInMemoryCollection(values);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                if (File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                }
            }
        }
    }
}
