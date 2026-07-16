using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MarineInsight.Web.Observability;

public static class MarineInsightTelemetry
{
    public const string ActivitySourceName = "MarineInsight";
    public const string ServiceName = "marine-insight-web";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static IServiceCollection AddMarineInsightTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var serviceVersion = typeof(MarineInsightTelemetry).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        var otlpEndpoint = TryGetOtlpEndpoint(configuration);

        var openTelemetry = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: ServiceName,
                serviceVersion: serviceVersion,
                serviceInstanceId: Environment.MachineName));

        openTelemetry.WithTracing(tracing =>
        {
            tracing
                .AddSource(ActivitySourceName)
                .AddAspNetCoreInstrumentation(options => options.RecordException = true)
                .AddHttpClientInstrumentation(options => options.RecordException = true);

            if (otlpEndpoint is not null)
            {
                tracing.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
            }
        });

        openTelemetry.WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();

            if (otlpEndpoint is not null)
            {
                metrics.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
            }
        });

        return services;
    }

    public static Uri? TryGetOtlpEndpoint(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var endpointValue = configuration["OpenTelemetry:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpointValue)
            || !Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("http" or "https"))
        {
            return null;
        }

        return endpoint;
    }
}
