using MarineInsight.Application.Admin;
using MarineInsight.Application.Admin.Ports;
using MarineInsight.Domain.Location;
using MarineInsight.Infrastructure.Persistence;
using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Tests;

public sealed class AdminLocationRepositoryTests
{
    private static readonly Guid ActorUserId = Guid.NewGuid();

    private static readonly CreateLocationCommand ValidCommand = new(
        "枸杞岛",
        30.72,
        122.77,
        "Asia/Shanghai",
        LocationType.Island,
        CoastOrientationDeg: 45);

    [Fact]
    public async Task AddPersistsLocationAndWritesAudit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var repository = new AdminLocationRepository(dbContext, TimeProvider.System);
        var created = await repository.AddAsync(ActorUserId, ValidCommand);

        Assert.True(created.IsPreset);
        Assert.Equal("枸杞岛", created.DisplayName);
        Assert.Equal(45, created.CoastOrientationDeg);

        var entity = await dbContext.Locations.SingleAsync(location => location.Id == created.Id);
        Assert.True(entity.IsPreset);
        Assert.Equal("枸杞岛", entity.NormalizedName);
        Assert.Equal((decimal)30.72, entity.Latitude);
        Assert.Equal((decimal)122.77, entity.Longitude);

        var audit = await dbContext.AuditLogs.SingleAsync(entry => entry.EventType == "location.created");
        Assert.Equal(ActorUserId, audit.ActorUserId);
        Assert.Equal("Location", audit.TargetType);
        Assert.Contains("枸杞岛", audit.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdatePersistsChangesAndWritesAudit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var repository = new AdminLocationRepository(dbContext, TimeProvider.System);
        var created = await repository.AddAsync(ActorUserId, ValidCommand);

        var updated = await repository.UpdateAsync(
            ActorUserId,
            created.Id,
            new UpdateLocationCommand("东极岛", 30.19, 122.68, "Asia/Shanghai", LocationType.Island, CoastOrientationDeg: null));

        Assert.NotNull(updated);
        Assert.Equal("东极岛", updated.DisplayName);
        Assert.Null(updated.CoastOrientationDeg);

        var audit = await dbContext.AuditLogs.SingleAsync(entry => entry.EventType == "location.updated");
        Assert.Contains("东极岛", audit.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteRemovesLocationAndReportsCascadedFavoriteCount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var repository = new AdminLocationRepository(dbContext, TimeProvider.System);
        var created = await repository.AddAsync(ActorUserId, ValidCommand);
        var favoriteOwner = Guid.NewGuid();
        dbContext.Users.Add(new MarineInsightUser
        {
            Id = favoriteOwner,
            UserName = "owner@example.com",
            Email = "owner@example.com"
        });
        await dbContext.SaveChangesAsync();
        dbContext.FavoriteLocations.Add(new FavoriteLocationEntity
        {
            Id = Guid.NewGuid(),
            UserId = favoriteOwner,
            LocationId = created.Id,
            DisplayName = "枸杞岛",
            SortOrder = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        // 实际请求中删除发生在独立 DI 作用域；用新的 DbContext 模拟，避免与 AddAsync 重复追踪同一主键。
        await using var deleteDbContext = CreateDbContext(connection);
        var deleteRepository = new AdminLocationRepository(deleteDbContext, TimeProvider.System);
        var result = await deleteRepository.DeleteAsync(ActorUserId, created.Id);

        Assert.NotNull(result);
        Assert.True(result.Deleted);
        Assert.Equal(1, result.CascadedFavoriteCount);
        Assert.False(await deleteDbContext.Locations.AnyAsync(location => location.Id == created.Id));

        var audit = await deleteDbContext.AuditLogs.SingleAsync(entry => entry.EventType == "location.deleted");
        Assert.Contains("级联删除 1 条收藏", audit.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteWhenReferencedByForecastBatchThrows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var repository = new AdminLocationRepository(dbContext, TimeProvider.System);
        var created = await repository.AddAsync(ActorUserId, ValidCommand);
        dbContext.ForecastBatches.Add(new ForecastBatchEntity
        {
            Id = Guid.NewGuid(),
            LocationId = created.Id,
            ProviderCode = "test-provider",
            DataDomain = 1,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            RangeStartUtc = DateTimeOffset.UtcNow,
            RangeEndUtc = DateTimeOffset.UtcNow.AddHours(24),
            QualityStatus = 1,
            Freshness = 1,
            Completeness = 1,
            RequestedLatitude = 30.72m,
            RequestedLongitude = 122.77m
        });
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<AdminLocationInUseException>(() =>
            repository.DeleteAsync(ActorUserId, created.Id));

        Assert.True(await dbContext.Locations.AnyAsync(location => location.Id == created.Id));
    }

    [Fact]
    public async Task DeleteReturnsNullForMissingLocation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var repository = new AdminLocationRepository(dbContext, TimeProvider.System);

        var result = await repository.DeleteAsync(ActorUserId, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ExistsByNormalizedCoordinatesDetectsDuplicate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var repository = new AdminLocationRepository(dbContext, TimeProvider.System);
        var created = await repository.AddAsync(ActorUserId, ValidCommand);

        var duplicate = await repository.ExistsByNormalizedCoordinatesAsync("枸杞岛", 30.72, 122.77, Guid.Empty);
        Assert.True(duplicate);

        var selfExcluded = await repository.ExistsByNormalizedCoordinatesAsync("枸杞岛", 30.72, 122.77, created.Id);
        Assert.False(selfExcluded);
    }

    private static MarineInsightDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MarineInsightDbContext>()
            .UseSqlite(connection)
            .Options;
        return new MarineInsightDbContext(options);
    }
}
