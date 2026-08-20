using MarineInsight.Application.Credentials;
using MarineInsight.Application.Credentials.Ports;

namespace MarineInsight.Application.Tests;

public sealed class ProviderCredentialServiceTests
{
    private static readonly Guid ActorUserId = Guid.NewGuid();
    private const string ProviderName = "worldtides";
    private const string ValidKey = "0123456789abcdef";

    [Fact]
    public async Task AddRejectsEmptyActor()
    {
        var service = new ProviderCredentialService(new FakeProviderCredentialStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddAsync(Guid.Empty, ProviderName, ValidKey));
    }

    [Fact]
    public async Task AddRejectsMissingProviderName()
    {
        var service = new ProviderCredentialService(new FakeProviderCredentialStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddAsync(ActorUserId, "  ", ValidKey));
    }

    [Fact]
    public async Task AddRejectsEmptyApiKey()
    {
        var service = new ProviderCredentialService(new FakeProviderCredentialStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddAsync(ActorUserId, ProviderName, "   "));
    }

    [Fact]
    public async Task AddRejectsTooShortApiKey()
    {
        var service = new ProviderCredentialService(new FakeProviderCredentialStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddAsync(ActorUserId, ProviderName, "short"));
    }

    [Fact]
    public async Task AddRejectsApiKeyWithControlCharacters()
    {
        var service = new ProviderCredentialService(new FakeProviderCredentialStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddAsync(ActorUserId, ProviderName, "ab\tcd12345678"));
    }

    [Fact]
    public async Task AddTrimsAndDelegatesToStore()
    {
        var store = new FakeProviderCredentialStore();
        var service = new ProviderCredentialService(store);

        await service.AddAsync(ActorUserId, ProviderName, $"  {ValidKey}  ");

        Assert.Equal(ProviderName, store.LastProviderName);
        Assert.Equal(ValidKey, store.LastApiKey);
    }

    [Fact]
    public async Task SetActiveRejectsEmptyActor()
    {
        var service = new ProviderCredentialService(new FakeProviderCredentialStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SetActiveAsync(Guid.Empty, ProviderName, Guid.NewGuid()));
    }

    [Fact]
    public async Task SetActiveRejectsEmptyKeyId()
    {
        var service = new ProviderCredentialService(new FakeProviderCredentialStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SetActiveAsync(ActorUserId, ProviderName, Guid.Empty));
    }

    [Fact]
    public async Task SetActiveDelegatesToStore()
    {
        var store = new FakeProviderCredentialStore();
        var service = new ProviderCredentialService(store);
        var keyId = Guid.NewGuid();

        await service.SetActiveAsync(ActorUserId, ProviderName, keyId);

        Assert.Equal(keyId, store.LastKeyId);
    }

    [Fact]
    public async Task DeleteRejectsEmptyKeyId()
    {
        var service = new ProviderCredentialService(new FakeProviderCredentialStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DeleteAsync(ActorUserId, ProviderName, Guid.Empty));
    }

    [Fact]
    public async Task DeleteDelegatesToStore()
    {
        var store = new FakeProviderCredentialStore();
        var service = new ProviderCredentialService(store);
        var keyId = Guid.NewGuid();

        await service.DeleteAsync(ActorUserId, ProviderName, keyId);

        Assert.Equal(keyId, store.LastDeletedKeyId);
    }

    private sealed class FakeProviderCredentialStore : IProviderCredentialStore
    {
        public string? LastProviderName { get; private set; }

        public string? LastApiKey { get; private set; }

        public Guid? LastKeyId { get; private set; }

        public Guid? LastDeletedKeyId { get; private set; }

        public Task<IReadOnlyList<ProviderCredentialSummary>> ListAsync(
            string providerName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProviderCredentialSummary>>([]);

        public Task AddAsync(
            Guid actorUserId,
            string providerName,
            string apiKey,
            CancellationToken cancellationToken = default)
        {
            LastProviderName = providerName;
            LastApiKey = apiKey;
            return Task.CompletedTask;
        }

        public Task SetActiveAsync(
            Guid actorUserId,
            string providerName,
            Guid keyId,
            CancellationToken cancellationToken = default)
        {
            LastKeyId = keyId;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            Guid actorUserId,
            string providerName,
            Guid keyId,
            CancellationToken cancellationToken = default)
        {
            LastDeletedKeyId = keyId;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProviderCredentialSecret>> ListSecretsAsync(
            string providerName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProviderCredentialSecret>>([]);

        public Task ReportHealthAsync(
            Guid? keyId,
            bool success,
            int? remainingCredits,
            bool creditWarning,
            string? failureReason,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
