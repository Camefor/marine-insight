using MarineInsight.Application.Analysis;
using MarineInsight.Application.Analysis.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Providers.Explanation;

public static class ExplanationServiceCollectionExtensions
{
    public static IServiceCollection AddExplanationProvider(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ExplanationOptions>()
            .Bind(configuration.GetSection(ExplanationOptions.SectionName))
            .Validate(options =>
            {
                try { options.Validate(); return true; }
                catch (InvalidOperationException) { return false; }
            }, "AI contains an invalid configuration.")
            .ValidateOnStart();

        services.AddHttpClient<OpenAiCompatibleExplanationProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddTransient<IExplanationProvider>(provider => provider.GetRequiredService<OpenAiCompatibleExplanationProvider>());
        services.AddSingleton<IExplanationCache, MemoryExplanationCache>();
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<ExplanationOptions>>().Value;
            return new ExplanationCachePolicy(options.CacheLifetime);
        });

        return services;
    }
}
