using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Persistence;

public static class DatabaseProviderExtensions
{
    public static DbContextOptionsBuilder UseMarineInsightDatabase(
        this DbContextOptionsBuilder optionsBuilder,
        DatabaseProviderKind provider,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A database connection string is required.", nameof(connectionString));
        }

        var migrationsAssembly = typeof(MarineInsightDbContext).Assembly.GetName().Name;

        return provider switch
        {
            DatabaseProviderKind.Sqlite => optionsBuilder.UseSqlite(
                connectionString,
                sqlite => sqlite.MigrationsAssembly(migrationsAssembly)),
            DatabaseProviderKind.PostgreSql => optionsBuilder.UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsAssembly(migrationsAssembly)),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported database provider.")
        };
    }
}
