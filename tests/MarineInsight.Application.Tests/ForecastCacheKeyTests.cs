using MarineInsight.Application.Forecast;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Tests;

public sealed class ForecastCacheKeyTests
{
    [Fact]
    public void CreateNormalizesGridCoordinatesAndBuildsSemanticKey()
    {
        var key = ForecastCacheKey.Create(
            "Production",
            ForecastDataDomain.Weather,
            new ProviderIdentity("Open-Meteo", "Best Model"),
            new GeoPoint(30.12344, 122.98765),
            new ForecastRange(new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), 24),
            "Normalizer-v1",
            coordinatePrecision: 4);

        Assert.Equal("production", key.Environment);
        Assert.Equal(new GeoPoint(30.1234, 122.9877), key.GridLocation);
        Assert.Equal(
            "mi:production:forecast:weather:open-meteo:best%20model:30.1234:122.9877:2026071600:24:normalizer-v1",
            key.Value);
    }

    [Fact]
    public void CreateSeparatesProviderModelRangeAndNormalizerVersion()
    {
        var start = new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);
        var location = new GeoPoint(30.1234, 122.9876);
        var provider = new ProviderIdentity("provider", "model-a");

        var original = ForecastCacheKey.Create(
            "development",
            ForecastDataDomain.Weather,
            provider,
            location,
            new ForecastRange(start, 24),
            "v1");
        var otherModel = ForecastCacheKey.Create(
            "development",
            ForecastDataDomain.Weather,
            new ProviderIdentity("provider", "model-b"),
            location,
            new ForecastRange(start, 24),
            "v1");
        var otherRange = ForecastCacheKey.Create(
            "development",
            ForecastDataDomain.Weather,
            provider,
            location,
            new ForecastRange(start, 72),
            "v1");
        var otherVersion = ForecastCacheKey.Create(
            "development",
            ForecastDataDomain.Weather,
            provider,
            location,
            new ForecastRange(start, 24),
            "v2");

        Assert.NotEqual(original.Value, otherModel.Value);
        Assert.NotEqual(original.Value, otherRange.Value);
        Assert.NotEqual(original.Value, otherVersion.Value);
    }

    [Fact]
    public void CreateRejectsNonHourlyCacheRange()
    {
        var range = new ForecastRange(
            new DateTimeOffset(2026, 7, 16, 0, 30, 0, TimeSpan.Zero),
            24);

        Assert.Throws<ArgumentException>(() => ForecastCacheKey.Create(
            "development",
            ForecastDataDomain.Weather,
            new ProviderIdentity("provider", "model"),
            new GeoPoint(30, 122),
            range,
            "v1"));
    }
}
