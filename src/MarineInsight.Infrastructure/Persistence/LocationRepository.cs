using MarineInsight.Application.Locations.Ports;
using MarineInsight.Domain.Forecast;
using MarineInsight.Domain.Location;
using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Persistence;

public sealed class LocationRepository(MarineInsightDbContext dbContext) : ILocationRepository
{
    public async Task<Location?> GetByIdAsync(
        Guid locationId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Locations
            .AsNoTracking()
            .SingleOrDefaultAsync(location => location.Id == locationId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<Location>> SearchAsync(
        string normalizedQuery,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var pattern = $"%{EscapeLikePattern(normalizedQuery)}%";
        var entities = await dbContext.Locations
            .AsNoTracking()
            .Where(location =>
                location.IsPreset &&
                (EF.Functions.Like(location.NormalizedName, pattern, "\\") ||
                 EF.Functions.Like(location.DisplayName, pattern, "\\")))
            .OrderBy(location => location.DisplayName)
            .ThenBy(location => location.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToArray();
    }

    public async Task<IReadOnlyList<Location>> FindNearbyAsync(
        GeoPoint center,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // The preset catalog is intentionally small at this stage. Keeping the distance
        // calculation in the repository avoids coupling the domain to a spatial database.
        var entities = await dbContext.Locations
            .AsNoTracking()
            .Where(location => location.IsPreset)
            .ToListAsync(cancellationToken);

        return entities
            .Select(entity =>
            {
                var location = ToDomain(entity);
                return (Location: location, DistanceKm: CalculateDistanceKm(center, location.Coordinates));
            })
            .Where(candidate => candidate.DistanceKm <= radiusKm)
            .OrderBy(candidate => candidate.DistanceKm)
            .ThenBy(candidate => candidate.Location.DisplayName)
            .ThenBy(candidate => candidate.Location.Id)
            .Take(limit)
            .Select(candidate => candidate.Location)
            .ToArray();
    }

    private static Location ToDomain(LocationEntity entity)
    {
        if (!Enum.IsDefined(typeof(LocationType), entity.LocationType))
        {
            throw new InvalidOperationException(
                $"Location '{entity.Id}' has an unknown location type.");
        }

        return new Location(
            entity.Id,
            entity.NormalizedName,
            entity.DisplayName,
            (double)entity.Latitude,
            (double)entity.Longitude,
            entity.TimeZoneId,
            (LocationType)entity.LocationType,
            entity.CoastOrientationDeg is { } orientation ? (double)orientation : null,
            entity.IsPreset,
            entity.CreatedAtUtc);
    }

    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static double CalculateDistanceKm(GeoPoint first, GeoPoint second)
    {
        const double earthRadiusKm = 6371.0088;
        var latitudeDelta = ToRadians(second.Latitude - first.Latitude);
        var longitudeDelta = ToRadians(second.Longitude - first.Longitude);
        var firstLatitude = ToRadians(first.Latitude);
        var secondLatitude = ToRadians(second.Latitude);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
            Math.Cos(firstLatitude) * Math.Cos(secondLatitude) *
            Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        var centralAngle = 2 * Math.Atan2(
            Math.Sqrt(Math.Min(1, haversine)),
            Math.Sqrt(Math.Max(0, 1 - haversine)));

        return earthRadiusKm * centralAngle;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
