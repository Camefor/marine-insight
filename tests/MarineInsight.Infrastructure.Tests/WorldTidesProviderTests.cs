using System.Net;
using System.Text;
using MarineInsight.Application.Errors;
using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Providers.WorldTides;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Tests;

public sealed class WorldTidesProviderTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 8, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FetchedAtUtc = StartUtc.AddMinutes(-5);

    [Fact]
    public async Task ProviderNormalizesTidesWarnsOnCreditsAndCachesExactRange()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(ReadSample()));
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(httpClient, cache);
        var location = new GeoPoint(30.194, 122.687);

        var first = await provider.GetTidesAsync(location, new ForecastRange(StartUtc, 24), default);
        var cached = await provider.GetTidesAsync(location, new ForecastRange(StartUtc, 24), default);
        _ = await provider.GetTidesAsync(location, new ForecastRange(StartUtc.AddHours(1), 24), default);

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
    }

    [Fact]
    public async Task AuthenticationRateLimitAndCreditExhaustionUseProviderFailures()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var unauthorizedClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var unauthorized = CreateProvider(unauthorizedClient, cache);
        await Assert.ThrowsAsync<ProviderAuthenticationException>(() => unauthorized.GetTidesAsync(
            new GeoPoint(30.194, 122.687), new ForecastRange(StartUtc, 24), default));

        using var limitedClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        var limited = CreateProvider(limitedClient, cache);
        await Assert.ThrowsAsync<ProviderRateLimitedException>(() => limited.GetTidesAsync(
            new GeoPoint(31, 123), new ForecastRange(StartUtc, 24), default));

        using var quotaClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse("{\"status\":400,\"error\":\"Insufficient credits\"}")));
        var quota = CreateProvider(quotaClient, cache);
        var exception = await Assert.ThrowsAsync<ProviderException>(() => quota.GetTidesAsync(
            new GeoPoint(32, 124), new ForecastRange(StartUtc, 24), default));
        Assert.Equal(ProviderFailureKind.QuotaExceeded, exception.FailureKind);
    }

    private static WorldTidesProvider CreateProvider(HttpClient client, IMemoryCache cache) => new(
        client,
        Options.Create(new WorldTidesOptions
        {
            Enabled = true,
            BaseUrl = "https://worldtides.test/api/v3",
            ApiKey = "test-key",
            CacheLifetime = TimeSpan.FromHours(12),
            CreditWarningThreshold = 100
        }),
        cache,
        new FixedTimeProvider(FetchedAtUtc));

    private static string ReadSample() => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "TestData", "WorldTides", "tide-response.json"));

    private static HttpResponseMessage JsonResponse(string payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };

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
