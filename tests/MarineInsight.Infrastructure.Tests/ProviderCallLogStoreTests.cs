using MarineInsight.Application.ProviderCalls;
using MarineInsight.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Tests;

public sealed class ProviderCallLogStoreTests
{
    private static readonly Guid ActorUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 2, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task BeginAndCompletePersistSafeBillingFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        var store = new ProviderCallLogStore(dbContext, new FixedTimeProvider(Now));

        var id = await store.BeginAsync(CreateStart(ProviderCallOperations.TideForecast));
        await store.CompleteAsync(id, new CompleteProviderCallLog(true, 200, 2, 98, 125, null));

        var entity = await dbContext.ProviderCallLogs.AsNoTracking().SingleAsync();
        Assert.Equal(ProviderCallOutcomes.Succeeded, entity.Outcome);
        Assert.Equal(2, entity.CreditsUsed);
        Assert.Equal(98, entity.RemainingCredits);
        Assert.Equal("••••cdef", entity.CredentialHint);
        Assert.Equal(30.19, entity.LatitudeBucket);
        Assert.Equal(122.69, entity.LongitudeBucket);
        Assert.DoesNotContain("0123456789abcdef", entity.CredentialHint, StringComparison.Ordinal);
        Assert.NotNull(entity.CompletedAtUtc);
    }

    [Fact]
    public async Task SearchFiltersOperationOutcomeUserAndDateWithPaging()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        var store = new ProviderCallLogStore(dbContext, new FixedTimeProvider(Now));
        var matchingId = await store.BeginAsync(CreateStart(ProviderCallOperations.TideForecast));
        await store.CompleteAsync(matchingId, new CompleteProviderCallLog(false, 429, null, 50, 50, "RATE_LIMITED"));
        var otherId = await store.BeginAsync(CreateStart(ProviderCallOperations.CredentialValidation) with
        {
            ActorUserId = Guid.NewGuid()
        });
        await store.CompleteAsync(otherId, new CompleteProviderCallLog(true, 200, 1, 49, 30, null));

        var result = await store.SearchAsync(new ProviderCallLogFilter(
            "worldtides",
            ProviderCallOperations.TideForecast,
            ProviderCallOutcomes.Failed,
            ActorUserId,
            Now.AddMinutes(-1),
            Now.AddMinutes(1),
            1,
            10));

        var item = Assert.Single(result.Items);
        Assert.Equal(matchingId, item.Id);
        Assert.Equal(1, result.Total);
        Assert.Equal("RATE_LIMITED", item.ErrorCode);
    }

    private static StartProviderCallLog CreateStart(string operation) => new(
        ActorUserId,
        "worldtides",
        operation,
        Guid.NewGuid(),
        "••••cdef",
        30.19,
        122.69,
        Now,
        Now.AddDays(1),
        2,
        "0123456789abcdef0123456789abcdef");

    private static MarineInsightDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MarineInsightDbContext>()
            .UseSqlite(connection)
            .Options;
        return new MarineInsightDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
