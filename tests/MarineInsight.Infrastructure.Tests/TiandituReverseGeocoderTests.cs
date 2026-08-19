using System.Net;
using System.Text;
using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Providers.Tianditu;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Tests;

public sealed class TiandituReverseGeocoderTests
{
    [Fact]
    public async Task GetNearestPlaceNameReturnsFormattedAddress()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            """{"status":"0","result":{"formatted_address":"浙江省舟山市嵊泗县枸杞乡"}}"""));
        using var httpClient = new HttpClient(handler);
        var geocoder = new TiandituReverseGeocoder(httpClient, CreateOptions("test-key"));

        var name = await geocoder.GetNearestPlaceNameAsync(new GeoPoint(30.72, 122.77), default);

        Assert.Equal("浙江省舟山市嵊泗县枸杞乡", name);
        Assert.Contains("type=geocode", handler.LastRequestUri?.Query, StringComparison.Ordinal);
        Assert.Contains("tk=test-key", handler.LastRequestUri?.Query, StringComparison.Ordinal);
        Assert.Contains("postStr=", handler.LastRequestUri?.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingKeyReturnsNullWithoutCallingHttp()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        using var httpClient = new HttpClient(handler);
        var geocoder = new TiandituReverseGeocoder(httpClient, CreateOptions(serverKey: null));

        var name = await geocoder.GetNearestPlaceNameAsync(new GeoPoint(30.72, 122.77), default);

        Assert.Null(name);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task NonSuccessResponseReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        using var httpClient = new HttpClient(handler);
        var geocoder = new TiandituReverseGeocoder(httpClient, CreateOptions("test-key"));

        var name = await geocoder.GetNearestPlaceNameAsync(new GeoPoint(30.72, 122.77), default);

        Assert.Null(name);
    }

    [Fact]
    public async Task ErrorStatusOrMissingAddressReturnsNull()
    {
        var errorHandler = new StubHttpMessageHandler(_ => JsonResponse("""{"status":"1","msg":"invalid request"}"""));
        using var errorClient = new HttpClient(errorHandler);
        var errorGeocoder = new TiandituReverseGeocoder(errorClient, CreateOptions("test-key"));
        Assert.Null(await errorGeocoder.GetNearestPlaceNameAsync(new GeoPoint(30.72, 122.77), default));

        var emptyHandler = new StubHttpMessageHandler(_ => JsonResponse("""{"status":"0","result":{"formatted_address":""}}"""));
        using var emptyClient = new HttpClient(emptyHandler);
        var emptyGeocoder = new TiandituReverseGeocoder(emptyClient, CreateOptions("test-key"));
        Assert.Null(await emptyGeocoder.GetNearestPlaceNameAsync(new GeoPoint(30.72, 122.77), default));
    }

    private static IOptions<TiandituOptions> CreateOptions(string? serverKey) =>
        Options.Create(new TiandituOptions
        {
            BaseUrl = "https://api.tianditu.test",
            ServerKey = serverKey,
            Timeout = TimeSpan.FromSeconds(5)
        });

    private static HttpResponseMessage JsonResponse(string payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };

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
