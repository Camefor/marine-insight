using MarineInsight.Application.Admin;
using MarineInsight.Application.Admin.Ports;
using MarineInsight.Domain.Location;

namespace MarineInsight.Application.Tests;

public sealed class AdminLocationServiceTests
{
    private static readonly Guid ActorUserId = Guid.NewGuid();

    private static readonly CreateLocationCommand ValidCommand = new(
        "枸杞岛",
        30.72,
        122.77,
        "Asia/Shanghai",
        LocationType.Island,
        CoastOrientationDeg: 45,
        IsHomeDefault: false);

    [Fact]
    public async Task CreateRejectsMissingDisplayName()
    {
        var service = new AdminLocationService(new FakeAdminLocationRepository());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(ActorUserId, ValidCommand with { DisplayName = "   " }));

        Assert.Contains("Display name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRejectsMissingTimeZone()
    {
        var service = new AdminLocationService(new FakeAdminLocationRepository());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(ActorUserId, ValidCommand with { TimeZoneId = "" }));

        Assert.Contains("Time zone", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRejectsUndefinedLocationType()
    {
        var service = new AdminLocationService(new FakeAdminLocationRepository());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateAsync(ActorUserId, ValidCommand with { LocationType = (LocationType)99 }));
    }

    [Fact]
    public async Task CreateRejectsOutOfRangeCoastOrientation()
    {
        var service = new AdminLocationService(new FakeAdminLocationRepository());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateAsync(ActorUserId, ValidCommand with { CoastOrientationDeg = 360 }));
    }

    [Fact]
    public async Task CreateDelegatesToRepository()
    {
        var repository = new FakeAdminLocationRepository();
        var service = new AdminLocationService(repository);

        var created = await service.CreateAsync(ActorUserId, ValidCommand);

        Assert.Equal("枸杞岛", created.DisplayName);
        Assert.Equal(ActorUserId, repository.LastActorUserId);
        Assert.Single(repository.AddedCommands);
    }

    [Fact]
    public async Task CreateRejectsNameCoordinateConflict()
    {
        var repository = new FakeAdminLocationRepository { CoordinateConflict = true };
        var service = new AdminLocationService(repository);

        await Assert.ThrowsAsync<AdminLocationConflictException>(() =>
            service.CreateAsync(ActorUserId, ValidCommand));
    }

    [Fact]
    public async Task UpdateRejectsEmptyId()
    {
        var service = new AdminLocationService(new FakeAdminLocationRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(ActorUserId, Guid.Empty, ToUpdate(ValidCommand)));
    }

    [Fact]
    public async Task UpdateDelegatesToRepository()
    {
        var repository = new FakeAdminLocationRepository();
        var service = new AdminLocationService(repository);

        var result = await service.UpdateAsync(ActorUserId, repository.TargetId, ToUpdate(ValidCommand));

        Assert.NotNull(result);
        Assert.Equal("枸杞岛", result.DisplayName);
        Assert.Equal(repository.TargetId, repository.UpdatedId);
    }

    [Fact]
    public async Task UpdateReturnsNullWhenLocationMissing()
    {
        var repository = new FakeAdminLocationRepository { Missing = true };
        var service = new AdminLocationService(repository);

        var result = await service.UpdateAsync(ActorUserId, repository.TargetId, ToUpdate(ValidCommand));

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteRejectsEmptyId()
    {
        var service = new AdminLocationService(new FakeAdminLocationRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DeleteAsync(ActorUserId, Guid.Empty));
    }

    [Fact]
    public async Task DeleteDelegatesAndReportsCascadedCount()
    {
        var repository = new FakeAdminLocationRepository();
        var service = new AdminLocationService(repository);

        var result = await service.DeleteAsync(ActorUserId, repository.TargetId);

        Assert.NotNull(result);
        Assert.True(result.Deleted);
        Assert.Equal(2, result.CascadedFavoriteCount);
        Assert.Equal(repository.TargetId, repository.DeletedId);
    }

    [Fact]
    public async Task CountFavoriteReferencesRejectsEmptyId()
    {
        var service = new AdminLocationService(new FakeAdminLocationRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CountFavoriteReferencesAsync(Guid.Empty));
    }

    [Fact]
    public async Task CountFavoriteReferencesReturnsRepositoryCount()
    {
        var repository = new FakeAdminLocationRepository();
        var service = new AdminLocationService(repository);

        var count = await service.CountFavoriteReferencesAsync(repository.TargetId);

        Assert.Equal(2, count);
        Assert.Equal(repository.TargetId, repository.CountedId);
    }

    private static UpdateLocationCommand ToUpdate(CreateLocationCommand command) =>
        new(command.DisplayName, command.Latitude, command.Longitude, command.TimeZoneId, command.LocationType, command.CoastOrientationDeg, command.IsHomeDefault);

    private sealed class FakeAdminLocationRepository : IAdminLocationRepository
    {
        public bool CoordinateConflict { get; init; }

        public bool Missing { get; init; }

        public Guid TargetId { get; } = Guid.NewGuid();

        public Guid? LastActorUserId { get; private set; }

        public Guid? UpdatedId { get; private set; }

        public Guid? DeletedId { get; private set; }

        public Guid? CountedId { get; private set; }

        public List<CreateLocationCommand> AddedCommands { get; } = [];

        public Task<IReadOnlyList<Location>> ListPresetsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Location>>([]);

        public Task<bool> ExistsByNormalizedCoordinatesAsync(
            string normalizedName,
            double latitude,
            double longitude,
            Guid excludeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CoordinateConflict);

        public Task<int> CountFavoriteReferencesAsync(Guid id, CancellationToken cancellationToken = default)
        {
            CountedId = id;
            return Task.FromResult(2);
        }

        public Task<Location> AddAsync(
            Guid actorUserId,
            CreateLocationCommand command,
            CancellationToken cancellationToken = default)
        {
            LastActorUserId = actorUserId;
            AddedCommands.Add(command);
            return Task.FromResult(CreateLocation(command));
        }

        public Task<Location?> UpdateAsync(
            Guid actorUserId,
            Guid id,
            UpdateLocationCommand command,
            CancellationToken cancellationToken = default)
        {
            LastActorUserId = actorUserId;
            UpdatedId = id;
            if (Missing)
            {
                return Task.FromResult<Location?>(null);
            }

            return Task.FromResult<Location?>(CreateLocation(new CreateLocationCommand(
                command.DisplayName, command.Latitude, command.Longitude, command.TimeZoneId, command.LocationType, command.CoastOrientationDeg, command.IsHomeDefault)));
        }

        public Task<LocationDeleteResult?> DeleteAsync(
            Guid actorUserId,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            LastActorUserId = actorUserId;
            DeletedId = id;
            return Task.FromResult<LocationDeleteResult?>(new LocationDeleteResult(Deleted: true, CascadedFavoriteCount: 2));
        }

        private static Location CreateLocation(CreateLocationCommand command) => new(
            Guid.NewGuid(),
            command.DisplayName.Trim().ToLowerInvariant(),
            command.DisplayName,
            command.Latitude,
            command.Longitude,
            command.TimeZoneId,
            command.LocationType,
            command.CoastOrientationDeg,
            isPreset: true,
            DateTimeOffset.UtcNow);
    }
}
