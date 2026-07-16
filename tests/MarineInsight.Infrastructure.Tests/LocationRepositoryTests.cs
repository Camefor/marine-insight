using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Tests;

public sealed class LocationRepositoryTests
{
    [Fact]
    public async Task MigrationSeedsPresetLocationsForSearchAndNearbyQueries()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var repository = new LocationRepository(dbContext);

        var searchResults = await repository.SearchAsync("东极", 10);
        var nearbyResults = await repository.FindNearbyAsync(
            new GeoPoint(30.194, 122.687),
            2,
            10);

        var searchResult = Assert.Single(searchResults);
        Assert.Equal("东极岛", searchResult.DisplayName);
        Assert.True(searchResult.IsPreset);
        Assert.Equal("Asia/Shanghai", searchResult.TimeZoneId);
        Assert.Equal(searchResult.Id, Assert.Single(nearbyResults).Id);
    }

    [Fact]
    public async Task SearchExcludesNonPresetCatalogRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        dbContext.Locations.Add(new Persistence.Entities.LocationEntity
        {
            Id = Guid.NewGuid(),
            NormalizedName = "private-test-location",
            DisplayName = "Private test location",
            Latitude = 30,
            Longitude = 122,
            TimeZoneId = "Asia/Shanghai",
            LocationType = 0,
            IsPreset = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var repository = new LocationRepository(dbContext);
        var results = await repository.SearchAsync("private-test", 10);

        Assert.Empty(results);
    }

    private static MarineInsightDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MarineInsightDbContext>()
            .UseSqlite(connection)
            .Options;
        return new MarineInsightDbContext(options);
    }
}
