using MarineInsight.Application.Users;
using MarineInsight.Application.Users.Ports;
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
        var command = new SaveFavoriteCommand(locationId, ActivityType.ShoreFishing, "常用钓点", 2);

        var first = await fixture.Repository.AddFavoriteAsync(firstUser, command, default);
        var duplicate = await fixture.Repository.AddFavoriteAsync(firstUser, command, default);
        var second = await fixture.Repository.AddFavoriteAsync(secondUser, command, default);

        Assert.NotNull(first);
        Assert.Null(duplicate);
        Assert.NotNull(second);
        Assert.Single(await fixture.Repository.ListFavoritesAsync(firstUser, default));
        Assert.Single(await fixture.Repository.ListFavoritesAsync(secondUser, default));
        Assert.False(await fixture.Repository.DeleteFavoriteAsync(secondUser, first!.Id, default));
        Assert.True(await fixture.Repository.DeleteFavoriteAsync(firstUser, first.Id, default));
    }

    [Fact]
    public async Task HistoryAndSettingsRemainIsolatedByOwner()
    {
        await using var fixture = await WorkspaceFixture.CreateAsync();
        var firstUser = await fixture.CreateUserAsync("history@example.com");
        var secondUser = await fixture.CreateUserAsync("other@example.com");
        await fixture.Repository.RecordHistoryAsync(firstUser, new RecordQueryHistoryCommand(
            null, "自定义坐标", 30.1, 122.2, DateTimeOffset.Parse("2026-08-12T00:00:00Z"), 24,
            [ActivityType.Boat], Guid.NewGuid(), "good", 82), default);
        await fixture.Repository.SaveSettingsAsync(firstUser,
            new UserSettings("knot", "foot", "fahrenheit", ActivityType.Boat, "Asia/Shanghai"), default);

        Assert.Single(await fixture.Repository.ListHistoryAsync(firstUser, 50, default));
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

        public IUserWorkspaceRepository Repository { get; }

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
