using MarineInsight.Application.Forecast.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarineInsight.Infrastructure.Providers.WorldTides;

public static class WorldTidesServiceCollectionExtensions
{
    public static IServiceCollection AddWorldTidesProvider(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WorldTidesOptions>()
            .Bind(configuration.GetSection(WorldTidesOptions.SectionName))
            .Validate(options =>
            {
                try { options.Validate(); return true; }
                catch (InvalidOperationException) { return false; }
            }, "TideProviders:WorldTides contains an invalid configuration.")
            .ValidateOnStart();
        services.AddHttpClient<WorldTidesProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddTransient<ITideProvider>(provider => provider.GetRequiredService<WorldTidesProvider>());
        return services;
    }
}
