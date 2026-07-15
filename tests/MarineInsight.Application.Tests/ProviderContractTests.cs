using MarineInsight.Application.Errors;
using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Tests;

public sealed class ProviderContractTests
{
    [Fact]
    public void ProviderRateLimitedExceptionExposesRetrySemantics()
    {
        var retryAfter = DateTimeOffset.UtcNow.AddMinutes(1);
        var exception = new ProviderRateLimitedException("Open-Meteo", "Provider rate limit reached.", retryAfter);

        Assert.Equal(MarineInsightErrorCodes.RateLimited, exception.ErrorCode);
        Assert.Equal("open-meteo", exception.ProviderCode);
        Assert.Equal(ProviderFailureKind.RateLimited, exception.FailureKind);
        Assert.True(exception.IsTransient);
        Assert.Equal(retryAfter.ToUniversalTime(), exception.RetryAfterUtc);
    }

    [Fact]
    public void ProviderAuthenticationExceptionIsNotTransient()
    {
        var exception = new ProviderAuthenticationException("open-meteo", "Provider credentials were rejected.");

        Assert.Equal(MarineInsightErrorCodes.ProviderAuthenticationFailed, exception.ErrorCode);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public void ProviderForecastResultRejectsTideBatches()
    {
        var batch = CreateBatch(ForecastDataDomain.Tide);

        Assert.Throws<ArgumentException>(() => new ProviderForecastResult(batch));
    }

    [Fact]
    public async Task WeatherProviderPortReturnsAStandardizedBatch()
    {
        var provider = new FakeWeatherProvider(CreateBatch(ForecastDataDomain.Weather));
        var result = await provider.GetWeatherAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(DateTimeOffset.UtcNow, 24),
            CancellationToken.None);

        Assert.Equal(ForecastDataDomain.Weather, result.Batch.DataDomain);
        Assert.Equal("fake-weather", provider.ProviderCode);
    }

    [Fact]
    public void InsufficientForecastExceptionReportsMissingMetrics()
    {
        var exception = new InsufficientForecastException(new[]
        {
            ForecastMetricName.WaveHeightM,
            ForecastMetricName.SwellHeightM,
            ForecastMetricName.WaveHeightM
        });

        Assert.Equal(MarineInsightErrorCodes.ForecastInsufficient, exception.ErrorCode);
        Assert.Equal(2, exception.MissingMetrics.Count);
    }

    private static ForecastBatch CreateBatch(ForecastDataDomain dataDomain)
    {
        var batchId = Guid.NewGuid();
        var provider = new ProviderIdentity("fake-weather", "test-model");
        var start = DateTimeOffset.UtcNow;
        var range = new ForecastRange(start, 24);
        var point = new ForecastPoint(
            range.StartUtc,
            ForecastMetricSet.Create(),
            DataQuality.Valid(),
            Array.Empty<MetricSource>());

        return new ForecastBatch(
            batchId,
            dataDomain,
            provider,
            new GeoPoint(30.194, 122.687),
            null,
            start.AddHours(-1),
            start,
            range,
            new[] { point },
            DataQuality.Valid());
    }

    private sealed class FakeWeatherProvider(ForecastBatch batch) : IWeatherForecastProvider
    {
        public string ProviderCode => "fake-weather";

        public Task<ProviderForecastResult> GetWeatherAsync(
            GeoPoint location,
            ForecastRange range,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ProviderForecastResult(batch));
        }
    }
}
