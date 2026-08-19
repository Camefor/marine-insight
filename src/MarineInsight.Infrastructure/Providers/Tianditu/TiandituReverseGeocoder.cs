using System.Text.Json;
using System.Text.Json.Serialization;
using MarineInsight.Application.Locations.Ports;
using MarineInsight.Domain.Forecast;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Providers.Tianditu;

/// <summary>
/// 天地图逆地理编码：地图选点时反查最近地名填充地点名称。Best-effort，任何失败均返回 null。
/// </summary>
public sealed class TiandituReverseGeocoder(
    HttpClient httpClient,
    IOptions<TiandituOptions> options) : IReverseGeocoder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string?> GetNearestPlaceNameAsync(
        GeoPoint point,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ServerKey))
        {
            return null;
        }

        var query = new Dictionary<string, string?>
        {
            ["postStr"] = JsonSerializer.Serialize(new { lon = point.Longitude, lat = point.Latitude, ver = 1 }),
            ["type"] = "geocode",
            ["tk"] = settings.ServerKey
        };
        var uri = QueryHelpers.AddQueryString($"{settings.BaseUrl.TrimEnd('/')}/geocoder", query);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.Timeout);
        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var payload = await JsonSerializer.DeserializeAsync<TiandituGeocoderResponse>(stream, JsonOptions, timeout.Token);
            return payload is { Status: "0", Result.FormattedAddress: { Length: > 0 } address }
                ? address
                : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or JsonException or OperationCanceledException)
        {
            return null;
        }
    }

    private sealed record TiandituGeocoderResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("result")] TiandituGeocoderResult? Result);

    private sealed record TiandituGeocoderResult(
        [property: JsonPropertyName("formatted_address")] string FormattedAddress);
}
