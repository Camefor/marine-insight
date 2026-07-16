using MarineInsight.Application.Forecast.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarineInsight.Infrastructure.Providers.OpenMeteo;

public static class OpenMeteoServiceCollectionExtensions
{
    public static IServiceCollection AddOpenMeteoForecastProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<OpenMeteoOptions>()
            .Bind(configuration.GetSection(OpenMeteoOptions.SectionName))
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
                "ForecastProviders:OpenMeteo contains an invalid configuration.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<OpenMeteoWeatherProvider>(client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddHttpClient<OpenMeteoMarineProvider>(client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddTransient<IWeatherForecastProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenMeteoWeatherProvider>());
        services.AddTransient<IMarineForecastProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenMeteoMarineProvider>());

        return services;
    }
}
