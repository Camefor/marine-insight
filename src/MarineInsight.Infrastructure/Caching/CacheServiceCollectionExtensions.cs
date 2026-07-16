using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MarineInsight.Infrastructure.Caching;

public static class CacheServiceCollectionExtensions
{
    public static IServiceCollection AddMarineInsightCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ForecastCacheOptions>()
            .Bind(configuration.GetSection(ForecastCacheOptions.SectionName))
            .Validate(
                options =>
                {
                    try
                    {
                        options.Validate();
                        return true;
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }
                },
                "Caching:Forecast contains an invalid configuration.")
            .ValidateOnStart();

        services.AddMemoryCache();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IForecastBatchCache, MemoryForecastBatchCache>();
        services.AddSingleton<ForecastBatchCacheCoordinator>();
        services.AddSingleton<ForecastCacheKeyFactory>();
        // Keep the concrete registration available for diagnostics while the application layer consumes the port.
        services.AddSingleton<IForecastCacheKeyFactory>(
            serviceProvider => serviceProvider.GetRequiredService<ForecastCacheKeyFactory>());

        return services;
    }
}
