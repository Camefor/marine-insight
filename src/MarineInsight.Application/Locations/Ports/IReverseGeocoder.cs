using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Locations.Ports;

/// <summary>
/// 根据坐标反查最近地名的适配器边界；找不到或调用失败时返回 null，调用方不应因失败中断主流程。
/// </summary>
public interface IReverseGeocoder
{
    Task<string?> GetNearestPlaceNameAsync(
        GeoPoint point,
        CancellationToken cancellationToken = default);
}
