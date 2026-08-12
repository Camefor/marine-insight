using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarineInsight.Infrastructure.Tests;

public sealed class PersistenceMigrationTests
{
    [Fact]
    public void SqliteMigrationCreatesForecastStorageSchema()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MarineInsightDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new MarineInsightDbContext(options);
        dbContext.Database.Migrate();

        var tables = ReadNames(connection, "SELECT name FROM sqlite_master WHERE type = 'table';");
        var indexes = ReadNames(connection, "SELECT name FROM sqlite_master WHERE type = 'index';");

        Assert.Contains("locations", tables);
        Assert.Contains("forecast_batches", tables);
        Assert.Contains("forecast_points", tables);
        Assert.Contains("forecast_point_sources", tables);
        Assert.Contains("users", tables);
        Assert.Contains("roles", tables);
        Assert.Contains("user_claims", tables);
        Assert.Contains("user_logins", tables);
        Assert.Contains("user_roles", tables);
        Assert.Contains("user_tokens", tables);
        Assert.Contains("IX_forecast_points_batch_id_forecast_time", indexes);
        Assert.Contains("UserNameIndex", indexes);
        Assert.Contains("EmailIndex", indexes);
        Assert.Empty(dbContext.Database.GetPendingMigrations());
    }

    [Fact]
    public void PersistenceRegistrationUsesSqliteByDefault()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MarineInsight"] = "Data Source=:memory:"
            })
            .Build();

        services.AddMarineInsightPersistence(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MarineInsightDbContext>();

        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", dbContext.Database.ProviderName);
        Assert.IsType<ForecastBatchRepository>(
            scope.ServiceProvider.GetRequiredService<IForecastBatchRepository>());
    }

    [Fact]
    public void PersistenceRegistrationUsesConfiguredPostgreSql()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "PostgreSql",
                ["ConnectionStrings:MarineInsight"] = "Host=localhost;Database=marine_insight;Username=test;Password=test"
            })
            .Build();

        services.AddMarineInsightPersistence(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MarineInsightDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    private static HashSet<string> ReadNames(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
