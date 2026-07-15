using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarineInsight.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddMarineInsightPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("MarineInsight");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:MarineInsight must be configured before registering persistence.");
        }

        var providerName = configuration["Database:Provider"] ?? nameof(DatabaseProviderKind.Sqlite);
        if (!Enum.TryParse<DatabaseProviderKind>(providerName, ignoreCase: true, out var provider))
        {
            throw new InvalidOperationException($"Unsupported database provider '{providerName}'.");
        }

        services.AddDbContext<MarineInsightDbContext>(options =>
            options.UseMarineInsightDatabase(provider, connectionString));

        return services;
    }
}
