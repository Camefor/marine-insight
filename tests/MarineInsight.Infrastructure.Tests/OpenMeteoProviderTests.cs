using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using MarineInsight.Application.Errors;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Providers.OpenMeteo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Tests;

public sealed class OpenMeteoProviderTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FetchedAtUtc = new(2026, 7, 15, 23, 55, 0, TimeSpan.Zero);

    [Fact]
    public async Task WeatherProviderNormalizesUnitsTimeDirectionsAndGridLocation()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(ReadSample("weather-response.json")));
        using var httpClient = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(
            httpClient,
            Options.Create(CreateOptions()),
            new FixedTimeProvider(FetchedAtUtc));

        var result = await provider.GetWeatherAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(StartUtc, 24),
            CancellationToken.None);

        var batch = result.Batch;
        var firstPoint = batch.Points[0];

        Assert.Equal("open-meteo", provider.ProviderCode);
        Assert.Equal(ForecastDataDomain.Weather, batch.DataDomain);
        Assert.Equal("ecmwf_ifs025", batch.Provider.SourceModel);
        Assert.Equal(new GeoPoint(30.2, 122.7), batch.GridLocation);
        Assert.Equal(StartUtc, firstPoint.ForecastTimeUtc);
        Assert.Equal(5.5, firstPoint.Metrics.WindSpeedMs);
        Assert.Equal(0, firstPoint.Metrics.WindDirectionDeg);
        Assert.Equal(0.6, firstPoint.Metrics.PrecipitationMmPerHour);
        Assert.True(firstPoint.Metrics.Thunderstorm);
        Assert.Equal(ForecastQualityStatus.Valid, batch.Quality.Status);
        Assert.Equal(ForecastFreshness.Fresh, batch.Quality.Freshness);
        Assert.Equal(12, firstPoint.MetricSources.Count);
        Assert.All(firstPoint.MetricSources, source =>
        {
            Assert.Equal(batch.BatchId, source.BatchId);
            Assert.Equal(batch.Provider, source.Provider);
            Assert.Equal(firstPoint.ForecastTimeUtc, source.ForecastTimeUtc);
        });

        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("timezone=UTC", handler.LastRequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("wind_speed_unit=ms", handler.LastRequestUri.Query, StringComparison.Ordinal);
        Assert.Contains("start_date=2026-07-16", handler.LastRequestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MarineProviderKeepsMarineBatchSeparateAndNormalizesDirections()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(ReadSample("marine-response.json")));
        using var httpClient = new HttpClient(handler);
        var provider = new OpenMeteoMarineProvider(
            httpClient,
            Options.Create(CreateOptions()),
            new FixedTimeProvider(FetchedAtUtc));

        var result = await provider.GetMarineAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(StartUtc, 72),
            CancellationToken.None);

        var batch = result.Batch;
        var firstPoint = batch.Points[0];

        Assert.Equal(ForecastDataDomain.Marine, batch.DataDomain);
        Assert.Equal("best_match", batch.Provider.SourceModel);
        Assert.Equal(0.8, firstPoint.Metrics.WaveHeightM);
        Assert.Equal(7.5, firstPoint.Metrics.WavePeriodS);
        Assert.Equal(9.5, firstPoint.Metrics.WavePeakPeriodS);
        Assert.Equal(0, firstPoint.Metrics.WaveDirectionDeg);
        Assert.Equal(0.6, firstPoint.Metrics.SwellHeightM);
        Assert.Equal(12, firstPoint.MetricSources.Count);
        Assert.Equal(ForecastQualityStatus.Valid, batch.Quality.Status);
    }

    [Fact]
    public async Task MissingPeakPeriodsAreOptionalAndDoNotDegradeMarineQuality()
    {
        var sample = JsonNode.Parse(ReadSample("marine-response.json"))!.AsObject();
        var hourly = sample["hourly"]!.AsObject();
        hourly["wave_peak_period"]!.AsArray()[0] = null;
        hourly["wind_wave_peak_period"]!.AsArray()[0] = null;
        hourly["swell_wave_peak_period"]!.AsArray()[0] = null;

        var handler = new StubHttpMessageHandler(_ => JsonResponse(sample.ToJsonString()));
        using var httpClient = new HttpClient(handler);
        var provider = new OpenMeteoMarineProvider(
            httpClient,
            Options.Create(CreateOptions()),
            new FixedTimeProvider(FetchedAtUtc));

        var result = await provider.GetMarineAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(StartUtc, 72),
            CancellationToken.None);

        var firstPoint = result.Batch.Points[0];

        Assert.Null(firstPoint.Metrics.WavePeakPeriodS);
        Assert.Null(firstPoint.Metrics.WindWavePeakPeriodS);
        Assert.Null(firstPoint.Metrics.SwellPeakPeriodS);
        Assert.Equal(ForecastQualityStatus.Valid, firstPoint.Quality.Status);
        Assert.DoesNotContain(ForecastMetricName.WavePeakPeriodS, firstPoint.Quality.MissingMetrics);
        Assert.DoesNotContain(ForecastMetricName.WindWavePeakPeriodS, firstPoint.Quality.MissingMetrics);
        Assert.DoesNotContain(ForecastMetricName.SwellPeakPeriodS, firstPoint.Quality.MissingMetrics);
        Assert.Equal(9, firstPoint.MetricSources.Count);
    }

    [Fact]
    public async Task MissingArraysAndNullValuesBecomePartialWithoutZeroFilling()
    {
        var sample = JsonNode.Parse(ReadSample("weather-response.json"))!.AsObject();
        var hourly = sample["hourly"]!.AsObject();
        hourly.Remove("cape");
        hourly["wind_gusts_10m"]!.AsArray()[1] = null;

        var handler = new StubHttpMessageHandler(_ => JsonResponse(sample.ToJsonString()));
        using var httpClient = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(
            httpClient,
            Options.Create(CreateOptions()),
            new FixedTimeProvider(FetchedAtUtc));

        var result = await provider.GetWeatherAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(StartUtc, 24),
            CancellationToken.None);

        var point = result.Batch.Points[1];

        Assert.Null(point.Metrics.WindGustMs);
        Assert.Null(point.Metrics.CapeJkg);
        Assert.Equal(ForecastQualityStatus.Partial, point.Quality.Status);
        Assert.Contains(ForecastMetricName.WindGustMs, point.Quality.MissingMetrics);
        Assert.Contains(ForecastMetricName.CapeJkg, point.Quality.MissingMetrics);
        Assert.True(point.Quality.Flags.HasFlag(ForecastQualityMask.ModelUnsupported));
        Assert.True(point.Quality.Flags.HasFlag(ForecastQualityMask.MissingMetric));
        Assert.Equal(ForecastQualityStatus.Partial, result.Batch.Quality.Status);
    }

    [Fact]
    public async Task InvalidPhysicalValueIsMarkedInvalidAndNotPassedToDomain()
    {
        var sample = JsonNode.Parse(ReadSample("weather-response.json"))!.AsObject();
        var hourly = sample["hourly"]!.AsObject();
        hourly["wind_direction_10m"]!.AsArray()[1] = 400;

        var handler = new StubHttpMessageHandler(_ => JsonResponse(sample.ToJsonString()));
        using var httpClient = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(
            httpClient,
            Options.Create(CreateOptions()),
            new FixedTimeProvider(FetchedAtUtc));

        var result = await provider.GetWeatherAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(StartUtc, 24),
            CancellationToken.None);

        var point = result.Batch.Points[1];

        Assert.Null(point.Metrics.WindDirectionDeg);
        Assert.Equal(ForecastQualityStatus.Invalid, point.Quality.Status);
        Assert.True(point.Quality.Flags.HasFlag(ForecastQualityMask.InvalidValue));
    }

    [Fact]
    public async Task ArrayLengthMismatchIsAContractFailure()
    {
        var sample = JsonNode.Parse(ReadSample("marine-response.json"))!.AsObject();
        sample["hourly"]!.AsObject()["wave_height"]!.AsArray().RemoveAt(2);
        var handler = new StubHttpMessageHandler(_ => JsonResponse(sample.ToJsonString()));
        using var httpClient = new HttpClient(handler);
        var provider = new OpenMeteoMarineProvider(
            httpClient,
            Options.Create(CreateOptions()),
            new FixedTimeProvider(FetchedAtUtc));

        var exception = await Assert.ThrowsAsync<ProviderContractException>(() => provider.GetMarineAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(StartUtc, 24),
            CancellationToken.None));

        Assert.Equal("open-meteo", exception.ProviderCode);
        Assert.Equal(ProviderFailureKind.ContractViolation, exception.FailureKind);
    }

    [Fact]
    public async Task InvalidJsonIsMappedToContractFailure()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse("{ invalid json"));
        using var httpClient = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(
            httpClient,
            Options.Create(CreateOptions()),
            new FixedTimeProvider(FetchedAtUtc));

        var exception = await Assert.ThrowsAsync<ProviderContractException>(() => provider.GetWeatherAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(StartUtc, 24),
            CancellationToken.None));

        Assert.Equal(MarineInsightErrorCodes.ProviderContractInvalid, exception.ErrorCode);
    }

    [Fact]
    public async Task UnauthorizedResponseIsMappedToAuthenticationFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var httpClient = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(
            httpClient,
            Options.Create(CreateOptions()),
            new FixedTimeProvider(FetchedAtUtc));

        var exception = await Assert.ThrowsAsync<ProviderAuthenticationException>(() => provider.GetWeatherAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(StartUtc, 24),
            CancellationToken.None));

        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task RateLimitedResponseCarriesRetryAfter()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(20));
        var handler = new StubHttpMessageHandler(_ => response);
        using var httpClient = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(
            httpClient,
            Options.Create(CreateOptions()),
            new FixedTimeProvider(FetchedAtUtc));

        var exception = await Assert.ThrowsAsync<ProviderRateLimitedException>(() => provider.GetWeatherAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(StartUtc, 24),
            CancellationToken.None));

        Assert.Equal(FetchedAtUtc.AddSeconds(20), exception.RetryAfterUtc);
        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task ServerFailureIsTransientProviderFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        using var httpClient = new HttpClient(handler);
        var provider = new OpenMeteoMarineProvider(
            httpClient,
            Options.Create(CreateOptions()),
            new FixedTimeProvider(FetchedAtUtc));

        var exception = await Assert.ThrowsAsync<ProviderException>(() => provider.GetMarineAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(StartUtc, 24),
            CancellationToken.None));

        Assert.Equal(ProviderFailureKind.Unavailable, exception.FailureKind);
        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task TransportTimeoutIsMappedToTimeoutFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new OperationCanceledException());
        using var httpClient = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(
            httpClient,
            Options.Create(CreateOptions()),
            new FixedTimeProvider(FetchedAtUtc));

        var exception = await Assert.ThrowsAsync<ProviderTimeoutException>(() => provider.GetWeatherAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(StartUtc, 24),
            CancellationToken.None));

        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task DisabledProviderDoesNotCallHttpClient()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called."));
        using var httpClient = new HttpClient(handler);
        var options = CreateOptions();
        options.Enabled = false;
        var provider = new OpenMeteoWeatherProvider(
            httpClient,
            Options.Create(options),
            new FixedTimeProvider(FetchedAtUtc));

        var exception = await Assert.ThrowsAsync<ProviderException>(() => provider.GetWeatherAsync(
            new GeoPoint(30.194, 122.687),
            new ForecastRange(StartUtc, 24),
            CancellationToken.None));

        Assert.Equal(ProviderFailureKind.Unavailable, exception.FailureKind);
        Assert.False(exception.IsTransient);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public void ServiceRegistrationExposesIndependentWeatherAndMarinePorts()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForecastProviders:OpenMeteo:WeatherBaseUrl"] = "https://weather.test/v1/forecast",
                ["ForecastProviders:OpenMeteo:MarineBaseUrl"] = "https://marine.test/v1/marine"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddOpenMeteoForecastProviders(configuration);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<OpenMeteoWeatherProvider>(serviceProvider.GetRequiredService<IWeatherForecastProvider>());
        Assert.IsType<OpenMeteoMarineProvider>(serviceProvider.GetRequiredService<IMarineForecastProvider>());
    }

    private static OpenMeteoOptions CreateOptions() => new()
    {
        WeatherBaseUrl = "https://weather.test/v1/forecast",
        MarineBaseUrl = "https://marine.test/v1/marine",
        Timeout = TimeSpan.FromSeconds(5)
    };

    private static string ReadSample(string fileName) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "TestData", "OpenMeteo", fileName));

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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(request));
        }
    }
}
