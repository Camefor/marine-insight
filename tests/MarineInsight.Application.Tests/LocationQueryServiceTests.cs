using MarineInsight.Application.Locations;
using MarineInsight.Application.Locations.Ports;
using MarineInsight.Domain.Forecast;
using MarineInsight.Domain.Location;

namespace MarineInsight.Application.Tests;

public sealed class LocationQueryServiceTests
{
    [Fact]
    public async Task SearchNormalizesQueryAndDelegatesTheRequestedLimit()
    {
        var repository = new FakeLocationRepository();
        var service = new LocationQueryService(repository);

        var result = await service.SearchPresetsAsync("  Dongji-Island  ", 3);

        Assert.Empty(result);
        Assert.Equal("dongji-island", repository.SearchQuery);
        Assert.Equal(3, repository.SearchLimit);
    }

    [Fact]
    public async Task NearbyQueryRejectsAnUnboundedRadius()
    {
        var service = new LocationQueryService(new FakeLocationRepository());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.FindNearbyPresetsAsync(new GeoPoint(30, 122), 501));
    }

    private sealed class FakeLocationRepository : ILocationRepository
    {
        public string? SearchQuery { get; private set; }

        public int SearchLimit { get; private set; }

        public Task<Location?> GetByIdAsync(
            Guid locationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Location?>(null);

        public Task<Location?> GetHomeDefaultAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Location?>(null);

        public Task<IReadOnlyList<Location>> SearchAsync(
            string normalizedQuery,
            int limit,
            CancellationToken cancellationToken = default)
        {
            SearchQuery = normalizedQuery;
            SearchLimit = limit;
            return Task.FromResult<IReadOnlyList<Location>>([]);
        }

        public Task<IReadOnlyList<Location>> FindNearbyAsync(
            GeoPoint center,
            double radiusKm,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Location>>([]);
    }
}
