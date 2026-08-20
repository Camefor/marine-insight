using MarineInsight.Application.Admin.Ports;
using MarineInsight.Application.Analysis.Ports;
using MarineInsight.Application.Credentials.Ports;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Application.Locations.Ports;
using MarineInsight.Application.Operations.Ports;
using MarineInsight.Application.Users.Ports;
using Microsoft.AspNetCore.Identity;
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
        services
            .AddIdentityCore<MarineInsightUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = configuration.GetValue(
                    "Identity:RequireConfirmedEmail",
                    false);
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddSignInManager()
            .AddEntityFrameworkStores<MarineInsightDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<IAdminLocationRepository, AdminLocationRepository>();
        services.AddScoped<IProviderCredentialStore, ProviderCredentialStore>();
        services.AddScoped<IForecastBatchRepository, ForecastBatchRepository>();
        services.AddScoped<IAnalysisReportRepository, AnalysisReportRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IUserWorkspaceRepository, UserWorkspaceRepository>();
        services.AddScoped<IOperationalReadRepository, OperationalReadRepository>();

        return services;
    }
}
