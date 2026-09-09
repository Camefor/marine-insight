using MarineInsight.Application.Sharing;
using MarineInsight.Application.Sharing.Ports;

namespace MarineInsight.Application.Tests;

public sealed class ShareSnapshotServiceTests
{
    [Fact]
    public async Task CreateUsesConfiguredValidityAndCapsSettingsAtThirtyDays()
    {
        var now = new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);
        var repository = new FakeSnapshotRepository();
        var service = new ShareSnapshotService(
            repository,
            new FakeSettingsRepository(new ShareSettings(60)),
            new FixedTimeProvider(now));

        var result = await service.CreateAsync("{\"snapshotId\":\"test\"}");

        Assert.Equal(30, (result.ExpiresAtUtc - now).Days);
        Assert.Equal(result.Token, repository.Snapshot?.Token);
    }

    [Fact]
    public async Task ExpiredSnapshotIsNotReturned()
    {
        var now = new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);
        var repository = new FakeSnapshotRepository();
        var service = new ShareSnapshotService(repository, new FakeSettingsRepository(ShareSettings.Default), new FixedTimeProvider(now));
        await repository.SaveAsync(new ShareSnapshot("expired", "{}", now.AddDays(-2), now, now));
        Assert.Null(await service.GetAsync("expired"));
    }

    [Fact]
    public async Task UpdateRejectsValidityBeyondThirtyDays()
    {
        var service = new ShareSnapshotService(new FakeSnapshotRepository(), new FakeSettingsRepository(ShareSettings.Default), new FixedTimeProvider(DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.UpdateSettingsAsync(31));
    }

    private sealed class FakeSnapshotRepository : IShareSnapshotRepository
    {
        public ShareSnapshot? Snapshot { get; private set; }
        public Task SaveAsync(ShareSnapshot snapshot, CancellationToken cancellationToken = default) { Snapshot = snapshot; return Task.CompletedTask; }
        public Task<ShareSnapshot?> GetActiveAsync(string token, DateTimeOffset nowUtc, CancellationToken cancellationToken = default) => Task.FromResult(Snapshot is { } snapshot && snapshot.Token == token && snapshot.ExpiresAtUtc > nowUtc ? snapshot : null);
        public Task<int> DeleteExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            if (Snapshot is { ExpiresAtUtc: var expiresAt } && expiresAt <= nowUtc) { Snapshot = null; return Task.FromResult(1); }
            return Task.FromResult(0);
        }
    }

    private sealed class FakeSettingsRepository(ShareSettings settings) : IShareSettingsRepository
    {
        private ShareSettings _settings = settings;
        public Task<ShareSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);
        public Task<ShareSettings> SaveAsync(ShareSettings value, CancellationToken cancellationToken = default) { _settings = value; return Task.FromResult(value); }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
