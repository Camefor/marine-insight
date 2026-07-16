using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Tests;

public sealed class ForecastCacheTests
{
    [Fact]
    public async Task MemoryCachePreservesProviderTimestampAndCacheBoundaries()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new TestTimeProvider(now);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryForecastBatchCache(memoryCache, clock);
        var key = CreateKey();
        var batch = CreateBatch();
        var policy = new ForecastCachePolicy(TimeSpan.FromMinutes(15), TimeSpan.FromHours(2));

        await cache.SetAsync(key, batch, policy);
        var entry = await cache.GetAsync(key);

        Assert.NotNull(entry);
        Assert.Equal(batch.FetchedAtUtc, entry.Batch.FetchedAtUtc);
        Assert.Equal(now, entry.CachedAtUtc);
        Assert.Equal(now.AddMinutes(15), entry.FreshUntilUtc);
        Assert.Equal(now.AddHours(2).AddMinutes(15), entry.StaleUntilUtc);
        Assert.True(entry.IsFresh(now.AddMinutes(14)));
        Assert.True(entry.IsWithinStaleIfError(now.AddMinutes(30)));
    }

    [Fact]
    public void RegistrationProvidesMemoryCacheCoordinatorAndKeyFactory()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Caching:Forecast:Environment"] = "test",
                ["Caching:Forecast:NormalizerVersion"] = "v2",
                ["Caching:Forecast:CoordinatePrecision"] = "3",
                ["Caching:Forecast:FreshLifetime"] = "00:10:00",
                ["Caching:Forecast:StaleIfErrorLifetime"] = "01:00:00"
            })
            .Build();

        services.AddMarineInsightCaching(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ForecastCacheOptions>>().Value;

        Assert.Equal("test", options.Environment);
        Assert.Equal("v2", options.NormalizerVersion);
        Assert.IsType<MemoryForecastBatchCache>(
            scope.ServiceProvider.GetRequiredService<IForecastBatchCache>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ForecastBatchCacheCoordinator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ForecastCacheKeyFactory>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMemoryCache>());
    }

    private static ForecastCacheKey CreateKey() => ForecastCacheKey.Create(
        "test",
        ForecastDataDomain.Weather,
        new ProviderIdentity("provider", "model"),
        new GeoPoint(30, 122),
        new ForecastRange(new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), 24),
        "v1");

    private static ForecastBatch CreateBatch()
    {
        var batchId = Guid.NewGuid();
        var provider = new ProviderIdentity("provider", "model");
        var quality = DataQuality.Valid();
        var forecastTime = new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);
        var source = new MetricSource(
            ForecastMetricName.WindSpeedMs,
            provider,
            batchId,
            forecastTime,
            quality.Status,
            quality.Freshness);
        var point = new ForecastPoint(
            forecastTime,
            ForecastMetricSet.Create(windSpeedMs: 4),
            quality,
            [source]);
        var range = new ForecastRange(forecastTime, 24);

        return new ForecastBatch(
            batchId,
            ForecastDataDomain.Weather,
            provider,
            new GeoPoint(30, 122),
            null,
            forecastTime.AddHours(-1),
            forecastTime.AddHours(-1),
            range,
            [point],
            quality);
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
