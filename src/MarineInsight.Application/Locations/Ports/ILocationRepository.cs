using MarineInsight.Domain.Forecast;
using MarineInsight.Domain.Location;

namespace MarineInsight.Application.Locations.Ports;

/// <summary>
/// Read boundary for normalized locations. Implementations must not call external geocoding services.
/// </summary>
public interface ILocationRepository
{
    Task<Location?> GetByIdAsync(
        Guid locationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Location>> SearchAsync(
        string normalizedQuery,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Location>> FindNearbyAsync(
        GeoPoint center,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken = default);
}
