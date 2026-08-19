using MarineInsight.Application.Locations.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarineInsight.Infrastructure.Providers.Tianditu;

public static class TiandituServiceCollectionExtensions
{
    public static IServiceCollection AddTiandituReverseGeocoder(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TiandituOptions>()
            .Bind(configuration.GetSection(TiandituOptions.SectionName))
            .Validate(options =>
            {
                try { options.Validate(); return true; }
                catch (InvalidOperationException) { return false; }
            }, "Map:Tianditu contains an invalid configuration.")
            .ValidateOnStart();
        services.AddHttpClient<TiandituReverseGeocoder>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddTransient<IReverseGeocoder>(provider => provider.GetRequiredService<TiandituReverseGeocoder>());
        return services;
    }
}
