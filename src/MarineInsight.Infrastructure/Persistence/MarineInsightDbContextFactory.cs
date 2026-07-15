using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MarineInsight.Infrastructure.Persistence;

public sealed class MarineInsightDbContextFactory : IDesignTimeDbContextFactory<MarineInsightDbContext>
{
    public MarineInsightDbContext CreateDbContext(string[] args)
    {
        var providerName = Environment.GetEnvironmentVariable("Database__Provider")
            ?? nameof(DatabaseProviderKind.Sqlite);
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MarineInsight")
            ?? "Data Source=marine-insight.db";

        if (!Enum.TryParse<DatabaseProviderKind>(providerName, ignoreCase: true, out var provider))
        {
            throw new InvalidOperationException($"Unsupported database provider '{providerName}'.");
        }

        var options = new DbContextOptionsBuilder<MarineInsightDbContext>();
        options.UseMarineInsightDatabase(provider, connectionString);
        return new MarineInsightDbContext(options.Options);
    }
}
