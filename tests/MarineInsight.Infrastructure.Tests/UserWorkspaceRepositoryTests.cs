using MarineInsight.Application.Users;
using MarineInsight.Domain.Analysis;
using MarineInsight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Tests;

public sealed class UserWorkspaceRepositoryTests
{
    [Fact]
    public async Task FavoritesAreUniqueAndIsolatedByOwner()
    {
        await using var fixture = await WorkspaceFixture.CreateAsync();
        var firstUser = await fixture.CreateUserAsync("first@example.com");
        var secondUser = await fixture.CreateUserAsync("second@example.com");
        var locationId = Guid.Parse("8a477d67-73fa-4f43-b954-cd29d238a89d");
        var command = new SaveFavoriteCommand(locationId, null, 0, 0, ActivityType.ShoreFishing, "常用钓点", 2);

        var first = await fixture.Repository.AddFavoriteAsync(firstUser, command, default);
        var earlier = await fixture.Repository.AddFavoriteAsync(firstUser,
            command with
            {
                LocationId = Guid.Parse("70cfb8c4-7af7-4c43-8f38-9a27e7cc2de7"),
                SortOrder = 1
            }, default);
        var duplicate = await fixture.Repository.AddFavoriteAsync(firstUser, command, default);
        var second = await fixture.Repository.AddFavoriteAsync(secondUser, command, default);

        Assert.NotNull(first);
        Assert.NotNull(earlier);
        Assert.Null(duplicate);
        Assert.NotNull(second);
        var firstUserFavorites = await fixture.Repository.ListFavoritesAsync(firstUser, default);
        Assert.Equal(2, firstUserFavorites.Count);
        Assert.Equal(earlier!.Id, firstUserFavorites[0].Id);
        Assert.Equal(first.Id, firstUserFavorites[1].Id);
        Assert.Single(await fixture.Repository.ListFavoritesAsync(secondUser, default));
        Assert.False(await fixture.Repository.DeleteFavoriteAsync(secondUser, first!.Id, default));
        Assert.True(await fixture.Repository.DeleteFavoriteAsync(firstUser, first.Id, default));
    }

    [Fact]
    public async Task MapPointFavoritesStoreNameAndCoordinatesAndDedupeByCoordinate()
    {
        await using var fixture = await WorkspaceFixture.CreateAsync();
        var user = await fixture.CreateUserAsync("map@example.com");
        var command = new SaveFavoriteCommand(null, "我的钓点", 30.194, 122.687, ActivityType.Boat, null, 0);

        var first = await fixture.Repository.AddFavoriteAsync(user, command, default);
        var duplicate = await fixture.Repository.AddFavoriteAsync(user, command, default);
        var other = await fixture.Repository.AddFavoriteAsync(user, command with { Longitude = 122.688 }, default);

        Assert.NotNull(first);
        Assert.Null(duplicate);
        Assert.NotNull(other);

        var favorites = await fixture.Repository.ListFavoritesAsync(user, default);
        Assert.Equal(2, favorites.Count);

        var saved = favorites.Single(favorite => favorite.Id == first.Id);
        Assert.Null(saved.LocationId);
        Assert.Equal("我的钓点", saved.DisplayName);
        Assert.Equal(30.194, saved.Latitude, 6);
        Assert.Equal(122.687, saved.Longitude, 6);

        Assert.True(await fixture.Repository.DeleteFavoriteAsync(user, first.Id, default));
    }

    [Fact]
    public async Task HistoryAndSettingsRemainIsolatedByOwner()
    {
        await using var fixture = await WorkspaceFixture.CreateAsync();
        var firstUser = await fixture.CreateUserAsync("history@example.com");
        var secondUser = await fixture.CreateUserAsync("other@example.com");
        await fixture.Repository.RecordHistoryAsync(firstUser, new RecordQueryHistoryCommand(
            null, "自定义坐标", 30.1, 122.2, new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero), 24,
            [ActivityType.Boat], Guid.NewGuid(), "good", 82), default);
        await fixture.Repository.RecordHistoryAsync(firstUser, new RecordQueryHistoryCommand(
            null, "较新记录", 30.2, 122.3, new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero), 72,
            [ActivityType.Landing], Guid.NewGuid(), "caution", 68), default);
        await fixture.Repository.SaveSettingsAsync(firstUser,
            new UserSettings("knot", "foot", "fahrenheit", ActivityType.Boat, "Asia/Shanghai"), default);

        var history = await fixture.Repository.ListHistoryAsync(firstUser, 1, default);
        Assert.Single(history);
        Assert.Equal("较新记录", history[0].DisplayName);
        Assert.Empty(await fixture.Repository.ListHistoryAsync(secondUser, 50, default));
        Assert.Equal("knot", (await fixture.Repository.GetSettingsAsync(firstUser, default)).WindSpeedUnit);
        Assert.Equal(UserSettings.Default, await fixture.Repository.GetSettingsAsync(secondUser, default));
    }

    private sealed class WorkspaceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private WorkspaceFixture(SqliteConnection connection, MarineInsightDbContext dbContext)
        {
            _connection = connection;
            DbContext = dbContext;
            Repository = new UserWorkspaceRepository(dbContext, TimeProvider.System);
        }

        public MarineInsightDbContext DbContext { get; }

        public UserWorkspaceRepository Repository { get; }

        public static async Task<WorkspaceFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new MarineInsightDbContext(new DbContextOptionsBuilder<MarineInsightDbContext>()
                .UseSqlite(connection)
                .Options);
            await context.Database.MigrateAsync();
            return new WorkspaceFixture(connection, context);
        }

        public async Task<Guid> CreateUserAsync(string email)
        {
            var user = new MarineInsightUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                SecurityStamp = Guid.NewGuid().ToString()
            };
            DbContext.Users.Add(user);
            await DbContext.SaveChangesAsync();
            return user.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
