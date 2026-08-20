using MarineInsight.Application.Credentials;
using MarineInsight.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Tests;

public sealed class ProviderCredentialStoreTests
{
    private static readonly Guid ActorUserId = Guid.NewGuid();
    private const string ProviderName = "worldtides";
    private const string ApiKey = "0123456789abcdef";

    [Fact]
    public async Task AddStoresEncryptedKeyAndActivatesFirstCredential()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        var store = CreateStore(dbContext);

        await store.AddAsync(ActorUserId, ProviderName, ApiKey);

        var entity = await dbContext.ProviderCredentials.SingleAsync(credential => credential.ProviderName == ProviderName);
        Assert.True(entity.IsActive);
        Assert.NotEqual(ApiKey, entity.EncryptedValue);
        Assert.Equal("••••cdef", entity.KeyHint);

        var secret = Assert.Single(await store.ListSecretsAsync(ProviderName));
        Assert.Equal(ApiKey, secret.ApiKey);
        Assert.True(secret.IsActive);

        var audit = await dbContext.AuditLogs.SingleAsync(entry => entry.EventType == "provider.credential.added");
        Assert.Equal(ActorUserId, audit.ActorUserId);
        Assert.Contains("cdef", audit.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetActiveMakesTargetActiveAndClearsOthers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        var store = CreateStore(dbContext);
        await store.AddAsync(ActorUserId, ProviderName, ApiKey);
        await store.AddAsync(ActorUserId, ProviderName, "fedcba9876543210");
        var first = await dbContext.ProviderCredentials.AsNoTracking().SingleAsync(credential => credential.KeyHint == "••••cdef");
        var second = await dbContext.ProviderCredentials.AsNoTracking().SingleAsync(credential => credential.KeyHint == "••••3210");

        await store.SetActiveAsync(ActorUserId, ProviderName, second.Id);

        Assert.False(await dbContext.ProviderCredentials.AnyAsync(credential => credential.Id == first.Id && credential.IsActive));
        Assert.True(await dbContext.ProviderCredentials.AnyAsync(credential => credential.Id == second.Id && credential.IsActive));

        var audit = await dbContext.AuditLogs.SingleAsync(entry => entry.EventType == "provider.credential.activated");
        Assert.Contains("3210", audit.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetActiveThrowsWhenCredentialMissing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        var store = CreateStore(dbContext);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SetActiveAsync(ActorUserId, ProviderName, Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteActiveCredentialWhileOthersExistThrows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        var store = CreateStore(dbContext);
        await store.AddAsync(ActorUserId, ProviderName, ApiKey);
        await store.AddAsync(ActorUserId, ProviderName, "fedcba9876543210");
        var active = await dbContext.ProviderCredentials.AsNoTracking().SingleAsync(credential => credential.IsActive);

        await Assert.ThrowsAsync<ProviderCredentialInUseException>(() =>
            store.DeleteAsync(ActorUserId, ProviderName, active.Id));

        Assert.Equal(2, await dbContext.ProviderCredentials.CountAsync(credential => credential.ProviderName == ProviderName));
    }

    [Fact]
    public async Task DeleteInactiveCredentialSucceeds()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        var store = CreateStore(dbContext);
        await store.AddAsync(ActorUserId, ProviderName, ApiKey);
        await store.AddAsync(ActorUserId, ProviderName, "fedcba9876543210");
        var inactive = await dbContext.ProviderCredentials.AsNoTracking().SingleAsync(credential => !credential.IsActive);

        await store.DeleteAsync(ActorUserId, ProviderName, inactive.Id);

        Assert.Single(await dbContext.ProviderCredentials.Where(credential => credential.ProviderName == ProviderName).ToListAsync());
        var audit = await dbContext.AuditLogs.SingleAsync(entry => entry.EventType == "provider.credential.deleted");
        Assert.Contains("3210", audit.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteLastActiveCredentialSucceeds()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        var store = CreateStore(dbContext);
        await store.AddAsync(ActorUserId, ProviderName, ApiKey);
        var active = await dbContext.ProviderCredentials.AsNoTracking().SingleAsync(credential => credential.IsActive);

        await store.DeleteAsync(ActorUserId, ProviderName, active.Id);

        Assert.Empty(await dbContext.ProviderCredentials.ToListAsync());
    }

    [Fact]
    public async Task ReportHealthUpdatesHealthCreditsAndFailureReason()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        var store = CreateStore(dbContext);
        await store.AddAsync(ActorUserId, ProviderName, ApiKey);
        var entity = await dbContext.ProviderCredentials.SingleAsync(credential => credential.ProviderName == ProviderName);

        await store.ReportHealthAsync(
            entity.Id,
            success: false,
            remainingCredits: 5,
            creditWarning: true,
            "WorldTides rejected the configured credential.");

        Assert.Equal(nameof(ProviderCredentialHealth.Failed), entity.Health);
        Assert.Equal(5, entity.RemainingCredits);
        Assert.True(entity.CreditWarning);
        Assert.NotNull(entity.LastCheckedAtUtc);
        Assert.Equal("WorldTides rejected the configured credential.", entity.LastFailureReason);
    }

    [Fact]
    public async Task ReportHealthClearsFailureReasonOnSuccess()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        var store = CreateStore(dbContext);
        await store.AddAsync(ActorUserId, ProviderName, ApiKey);
        var entity = await dbContext.ProviderCredentials.SingleAsync(credential => credential.ProviderName == ProviderName);
        await store.ReportHealthAsync(entity.Id, success: false, remainingCredits: 1, creditWarning: true, "boom");

        await store.ReportHealthAsync(entity.Id, success: true, remainingCredits: 250, creditWarning: false, null);

        Assert.Equal(nameof(ProviderCredentialHealth.Healthy), entity.Health);
        Assert.Equal(250, entity.RemainingCredits);
        Assert.False(entity.CreditWarning);
        Assert.Null(entity.LastFailureReason);
    }

    [Fact]
    public async Task ReportHealthWithNullKeyIdIsNoOp()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        var store = CreateStore(dbContext);

        // 配置兜底密钥无 KeyId，不记健康，不产生任何行。
        await store.ReportHealthAsync(null, success: false, remainingCredits: null, creditWarning: false, "x");

        Assert.Empty(await dbContext.ProviderCredentials.ToListAsync());
    }

    private static ProviderCredentialStore CreateStore(MarineInsightDbContext dbContext) =>
        new(dbContext, DataProtectionProvider.Create("test"), TimeProvider.System);

    private static MarineInsightDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MarineInsightDbContext>()
            .UseSqlite(connection)
            .Options;
        return new MarineInsightDbContext(options);
    }
}
