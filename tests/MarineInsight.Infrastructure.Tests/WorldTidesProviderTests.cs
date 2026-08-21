using System.Net;
using System.Text;
using MarineInsight.Application.Credentials;
using MarineInsight.Application.Credentials.Ports;
using MarineInsight.Application.Errors;
using MarineInsight.Application.ProviderCalls;
using MarineInsight.Application.ProviderCalls.Ports;
using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Providers.WorldTides;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Tests;

public sealed class WorldTidesProviderTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 8, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FetchedAtUtc = StartUtc.AddMinutes(-5);
    private static readonly Guid ActorUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task ProviderNormalizesTidesWarnsOnCreditsAndCachesExactRange()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(ReadSample()));
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var callLogs = new FakeProviderCallLogStore();
        var provider = CreateProvider(httpClient, cache, callLogStore: callLogs);
        var location = new GeoPoint(30.194, 122.687);

        var first = await provider.GetTidesAsync(location, new ForecastRange(StartUtc, 24), ActorUserId, default);
        var cached = await provider.GetTidesAsync(location, new ForecastRange(StartUtc, 24), ActorUserId, default);
        _ = await provider.GetTidesAsync(location, new ForecastRange(StartUtc.AddHours(1), 24), ActorUserId, default);

        Assert.True(provider.IsEnabled);
        Assert.Equal(2, handler.CallCount);
        Assert.False(first.FromCache);
        Assert.True(cached.FromCache);
        Assert.Equal(80, first.RemainingCredits);
        Assert.True(first.CreditWarning);
        Assert.Equal(ForecastDataDomain.Tide, first.Batch.DataDomain);
        Assert.Equal(new GeoPoint(30.2, 122.7), first.Batch.GridLocation);
        Assert.Equal(2, first.Batch.Points.Count);
        Assert.Equal(1.2, first.Batch.Points[0].Metrics.TideHeightM);
        Assert.Equal(TideType.High, first.Batch.Points[0].Metrics.TideType);
        Assert.Contains("key=test-key", handler.LastRequestUri?.Query, StringComparison.Ordinal);
        Assert.Equal(2, callLogs.Starts.Count);
        Assert.Equal(2, callLogs.Completions.Count);
        Assert.All(callLogs.Starts, item => Assert.Equal(ProviderCallOperations.TideForecast, item.Operation));
        Assert.All(callLogs.Completions, item => Assert.Equal(2, item.Command.CreditsUsed));
    }

    [Fact]
    public async Task AuthenticationRateLimitAndCreditExhaustionUseProviderFailures()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var unauthorizedClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var unauthorized = CreateProvider(unauthorizedClient, cache);
        await Assert.ThrowsAsync<ProviderAuthenticationException>(() => unauthorized.GetTidesAsync(
            new GeoPoint(30.194, 122.687), new ForecastRange(StartUtc, 24), ActorUserId, default));

        using var limitedClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        var limited = CreateProvider(limitedClient, cache);
        await Assert.ThrowsAsync<ProviderRateLimitedException>(() => limited.GetTidesAsync(
            new GeoPoint(31, 123), new ForecastRange(StartUtc, 24), ActorUserId, default));

        using var quotaClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse("{\"status\":400,\"error\":\"Insufficient credits\"}")));
        var quota = CreateProvider(quotaClient, cache);
        var exception = await Assert.ThrowsAsync<ProviderException>(() => quota.GetTidesAsync(
            new GeoPoint(32, 124), new ForecastRange(StartUtc, 24), ActorUserId, default));
        Assert.Equal(ProviderFailureKind.QuotaExceeded, exception.FailureKind);
    }

    [Fact]
    public async Task ActiveKeyFailureFallsBackToBackupKeyAndReportsHealth()
    {
        var activeId = Guid.NewGuid();
        var backupId = Guid.NewGuid();
        var store = new FakeCredentialStore
        {
            Secrets =
            [
                new ProviderCredentialSecret(activeId, "active-key", true),
                new ProviderCredentialSecret(backupId, "backup-key", false)
            ]
        };
        var handler = new StubHttpMessageHandler(request =>
            QueryKey(request.RequestUri!.Query) == "active-key"
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : JsonResponse(ReadSample()));
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var callLogs = new FakeProviderCallLogStore();
        var provider = CreateProvider(httpClient, cache, apiKey: null, store, callLogs);

        var result = await provider.GetTidesAsync(new GeoPoint(30.194, 122.687), new ForecastRange(StartUtc, 24), ActorUserId, default);

        Assert.Equal(2, handler.CallCount);
        Assert.Equal(2, callLogs.Starts.Count);
        Assert.Equal(2, callLogs.Completions.Count);
        Assert.Contains(callLogs.Completions, item => !item.Command.Succeeded && item.Command.HttpStatusCode == 401);
        Assert.Contains(callLogs.Completions, item => item.Command.Succeeded && item.Command.CreditsUsed == 2);
        Assert.Equal(80, result.RemainingCredits);
        Assert.Contains(store.HealthReports, report => report.KeyId == activeId && !report.Success && report.FailureReason == "WorldTides rejected the configured credential.");
        Assert.Contains(store.HealthReports, report => report.KeyId == backupId && report.Success && report.Credits == 80);
    }

    [Fact]
    public async Task AllCandidatesFailThrowsLastKeyFailure()
    {
        var store = new FakeCredentialStore
        {
            Secrets =
            [
                new ProviderCredentialSecret(Guid.NewGuid(), "active-key", true),
                new ProviderCredentialSecret(Guid.NewGuid(), "backup-key", false)
            ]
        };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var provider = CreateProvider(httpClient, cache, apiKey: null, store);

        await Assert.ThrowsAsync<ProviderAuthenticationException>(() =>
            provider.GetTidesAsync(new GeoPoint(30.194, 122.687), new ForecastRange(StartUtc, 24), ActorUserId, default));

        Assert.Equal(2, store.HealthReports.Count(report => !report.Success));
    }

    [Fact]
    public async Task NoCandidatesThrowsNotConfiguredWithoutNetworkCall()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(ReadSample()));
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(httpClient, cache, apiKey: null, new FakeCredentialStore());

        var exception = await Assert.ThrowsAsync<ProviderAuthenticationException>(() =>
            provider.GetTidesAsync(new GeoPoint(30.194, 122.687), new ForecastRange(StartUtc, 24), ActorUserId, default));

        Assert.Contains("not configured", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ConfigFallbackKeyIsUsedAndDoesNotReportHealth()
    {
        var store = new FakeCredentialStore();
        var handler = new StubHttpMessageHandler(_ => JsonResponse(ReadSample()));
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(httpClient, cache, apiKey: "test-key", store);

        var result = await provider.GetTidesAsync(new GeoPoint(30.194, 122.687), new ForecastRange(StartUtc, 24), ActorUserId, default);

        Assert.Equal(80, result.RemainingCredits);
        Assert.Equal("test-key", QueryKey(handler.LastRequestUri!.Query));
        Assert.Empty(store.HealthReports);
    }

    [Fact]
    public async Task ValidateKeyAsyncReportsSuccessForValidKey()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(ReadSample())));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var callLogs = new FakeProviderCallLogStore();
        var provider = CreateProvider(httpClient, cache, callLogStore: callLogs);

        var result = await provider.ValidateKeyAsync(ActorUserId, "valid-key", default);

        Assert.True(result.Success);
        Assert.Equal(80, result.RemainingCredits);
        Assert.Equal(ProviderCallOperations.CredentialValidation, Assert.Single(callLogs.Starts).Operation);
        Assert.Equal(2, Assert.Single(callLogs.Completions).Command.CreditsUsed);
    }

    [Fact]
    public async Task ValidateKeyAsyncReportsInvalidKeyOnUnauthorized()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(httpClient, cache);

        var result = await provider.ValidateKeyAsync(ActorUserId, "bad-key", default);

        Assert.False(result.Success);
        Assert.Contains("401", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateKeyAsyncReportsQuotaExhausted()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse("{\"status\":400,\"error\":\"Insufficient credits\"}")));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(httpClient, cache);

        var result = await provider.ValidateKeyAsync(ActorUserId, "quota-key", default);

        Assert.False(result.Success);
        Assert.Contains("额度", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateKeyAsyncRejectsEmptyKeyWithoutNetworkCall()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(ReadSample()));
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(httpClient, cache);

        var result = await provider.ValidateKeyAsync(ActorUserId, "   ", default);

        Assert.False(result.Success);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task PaidCallLogBeginFailurePreventsWorldTidesRequest()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(ReadSample()));
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var callLogs = new FakeProviderCallLogStore
        {
            BeginFailure = new InvalidOperationException("log unavailable")
        };
        var provider = CreateProvider(httpClient, cache, callLogStore: callLogs);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetTidesAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(StartUtc, 24),
            ActorUserId,
            default));

        Assert.Equal(0, handler.CallCount);
    }

    private static WorldTidesProvider CreateProvider(
        HttpClient client,
        IMemoryCache cache,
        string? apiKey = "test-key",
        FakeCredentialStore? store = null,
        FakeProviderCallLogStore? callLogStore = null) =>
        new(
            client,
            Options.Create(new WorldTidesOptions
            {
                Enabled = true,
                BaseUrl = "https://worldtides.test/api/v3",
                ApiKey = apiKey,
                CacheLifetime = TimeSpan.FromHours(12),
                CreditWarningThreshold = 100
            }),
            cache,
            store ?? new FakeCredentialStore(),
            callLogStore ?? new FakeProviderCallLogStore(),
            new FixedTimeProvider(FetchedAtUtc),
            NullLogger<WorldTidesProvider>.Instance);

    private static string ReadSample() => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "TestData", "WorldTides", "tide-response.json"));

    private static HttpResponseMessage JsonResponse(string payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };

    private static string QueryKey(string query)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts[0] == "key")
            {
                return parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            }
        }

        return string.Empty;
    }

    private sealed class FakeCredentialStore : IProviderCredentialStore
    {
        public IReadOnlyList<ProviderCredentialSecret> Secrets { get; init; } = [];

        public List<(Guid? KeyId, bool Success, int? Credits, string? FailureReason)> HealthReports { get; } = [];

        public Task<IReadOnlyList<ProviderCredentialSecret>> ListSecretsAsync(
            string providerName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Secrets);

        public Task ReportHealthAsync(
            Guid? keyId,
            bool success,
            int? remainingCredits,
            bool creditWarning,
            string? failureReason,
            CancellationToken cancellationToken = default)
        {
            // 与真实 Store 一致：配置兜底密钥（KeyId 为 null）不记健康。
            if (keyId is not null)
            {
                HealthReports.Add((keyId, success, remainingCredits, failureReason));
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProviderCredentialSummary>> ListAsync(
            string providerName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProviderCredentialSummary>>([]);

        public Task AddAsync(
            Guid actorUserId,
            string providerName,
            string apiKey,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetActiveAsync(
            Guid actorUserId,
            string providerName,
            Guid keyId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(
            Guid actorUserId,
            string providerName,
            Guid keyId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeProviderCallLogStore : IProviderCallLogStore
    {
        public Exception? BeginFailure { get; init; }

        public List<StartProviderCallLog> Starts { get; } = [];

        public List<(Guid Id, CompleteProviderCallLog Command)> Completions { get; } = [];

        public Task<Guid> BeginAsync(StartProviderCallLog command, CancellationToken cancellationToken = default)
        {
            if (BeginFailure is not null)
            {
                throw BeginFailure;
            }

            Starts.Add(command);
            return Task.FromResult(Guid.NewGuid());
        }

        public Task CompleteAsync(Guid id, CompleteProviderCallLog command, CancellationToken cancellationToken = default)
        {
            Completions.Add((id, command));
            return Task.CompletedTask;
        }

        public Task<ProviderCallLogPage> SearchAsync(ProviderCallLogFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderCallLogPage([], 0, filter.Page, filter.PageSize));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestUri = request.RequestUri;
            CallCount++;
            return Task.FromResult(responseFactory(request));
        }
    }
}
