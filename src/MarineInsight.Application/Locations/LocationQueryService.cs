using MarineInsight.Application.Locations.Ports;
using MarineInsight.Domain.Forecast;
using MarineInsight.Domain.Location;

namespace MarineInsight.Application.Locations;

/// <summary>
/// Validates location query parameters and delegates read-only catalog access to the repository.
/// </summary>
public sealed class LocationQueryService
{
    public const int DefaultLimit = 10;
    public const int MaxLimit = 50;
    public const double DefaultNearbyRadiusKm = 50;
    public const double MaxNearbyRadiusKm = 500;

    private readonly ILocationRepository _repository;

    public LocationQueryService(ILocationRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public Task<Location?> GetByIdAsync(
        Guid locationId,
        CancellationToken cancellationToken = default)
    {
        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("Location id cannot be empty.", nameof(locationId));
        }

        return _repository.GetByIdAsync(locationId, cancellationToken);
    }

    public Task<Location?> GetHomeDefaultAsync(CancellationToken cancellationToken = default) =>
        _repository.GetHomeDefaultAsync(cancellationToken);

    public Task<IReadOnlyList<Location>> SearchPresetsAsync(
        string query,
        int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeQuery(query);
        EnsureLimit(limit);

        return _repository.SearchAsync(normalizedQuery, limit, cancellationToken);
    }

    public Task<IReadOnlyList<Location>> FindNearbyPresetsAsync(
        GeoPoint center,
        double radiusKm = DefaultNearbyRadiusKm,
        int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(radiusKm) || radiusKm <= 0 || radiusKm > MaxNearbyRadiusKm)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radiusKm),
                radiusKm,
                $"Nearby radius must be greater than 0 and no more than {MaxNearbyRadiusKm} km.");
        }

        EnsureLimit(limit);
        return _repository.FindNearbyAsync(center, radiusKm, limit, cancellationToken);
    }

    private static string NormalizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Location search text is required.", nameof(query));
        }

        var normalized = query.Trim().ToLowerInvariant();
        if (normalized.Length > 160)
        {
            throw new ArgumentException("Location search text cannot exceed 160 characters.", nameof(query));
        }

        return normalized;
    }

    private static void EnsureLimit(int limit)
    {
        if (limit is < 1 or > MaxLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                $"Location result limit must be between 1 and {MaxLimit}.");
        }
    }
}
