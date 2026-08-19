using MarineInsight.Application.Locations.Ports;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Locations;

/// <summary>
/// 坐标反查最近地名：供地图选点时自动填充地点名称，结果缺失时返回 null，不影响坐标查询流程。
/// </summary>
public sealed class ReverseGeocodeService
{
    private readonly IReverseGeocoder _geocoder;

    public ReverseGeocodeService(IReverseGeocoder geocoder)
    {
        ArgumentNullException.ThrowIfNull(geocoder);
        _geocoder = geocoder;
    }

    public Task<string?> FindNearestNameAsync(
        GeoPoint point,
        CancellationToken cancellationToken = default) =>
        _geocoder.GetNearestPlaceNameAsync(point, cancellationToken);
}
