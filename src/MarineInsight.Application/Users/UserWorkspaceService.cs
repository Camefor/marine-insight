using MarineInsight.Application.Locations.Ports;
using MarineInsight.Application.Users.Ports;

namespace MarineInsight.Application.Users;

public sealed class UserWorkspaceService
{
    private static readonly HashSet<string> WindUnits = new(StringComparer.OrdinalIgnoreCase) { "mps", "kph", "knot" };
    private static readonly HashSet<string> WaveUnits = new(StringComparer.OrdinalIgnoreCase) { "meter", "foot" };
    private static readonly HashSet<string> TemperatureUnits = new(StringComparer.OrdinalIgnoreCase) { "celsius", "fahrenheit" };
    private readonly IUserWorkspaceRepository _repository;
    private readonly ILocationRepository _locations;

    public UserWorkspaceService(IUserWorkspaceRepository repository, ILocationRepository locations)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _locations = locations ?? throw new ArgumentNullException(nameof(locations));
    }

    public Task<IReadOnlyList<FavoriteLocation>> ListFavoritesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _repository.ListFavoritesAsync(RequireUser(userId), cancellationToken);

    public async Task<FavoriteLocation> AddFavoriteAsync(Guid userId, SaveFavoriteCommand command, CancellationToken cancellationToken = default)
    {
        ValidateFavorite(command);
        if (await _locations.GetByIdAsync(command.LocationId, cancellationToken) is null)
        {
            throw new KeyNotFoundException("The selected location does not exist.");
        }

        return await _repository.AddFavoriteAsync(RequireUser(userId), Normalize(command), cancellationToken)
            ?? throw new FavoriteAlreadyExistsException(command.LocationId);
    }

    public async Task<FavoriteLocation?> UpdateFavoriteAsync(Guid userId, Guid favoriteId, SaveFavoriteCommand command, CancellationToken cancellationToken = default)
    {
        ValidateFavorite(command);
        return await _repository.UpdateFavoriteAsync(RequireUser(userId), favoriteId, Normalize(command), cancellationToken);
    }

    public Task<bool> DeleteFavoriteAsync(Guid userId, Guid favoriteId, CancellationToken cancellationToken = default) =>
        _repository.DeleteFavoriteAsync(RequireUser(userId), favoriteId, cancellationToken);

    public Task<IReadOnlyList<QueryHistoryItem>> ListHistoryAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default) =>
        _repository.ListHistoryAsync(RequireUser(userId), Math.Clamp(limit, 1, 100), cancellationToken);

    public Task RecordHistoryAsync(Guid userId, RecordQueryHistoryCommand command, CancellationToken cancellationToken = default) =>
        _repository.RecordHistoryAsync(RequireUser(userId), command, cancellationToken);

    public Task<UserSettings> GetSettingsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _repository.GetSettingsAsync(RequireUser(userId), cancellationToken);

    public Task<UserSettings> SaveSettingsAsync(Guid userId, UserSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!WindUnits.Contains(settings.WindSpeedUnit) ||
            !WaveUnits.Contains(settings.WaveHeightUnit) ||
            !TemperatureUnits.Contains(settings.TemperatureUnit))
        {
            throw new ArgumentException("One or more unit preferences are unsupported.", nameof(settings));
        }

        if (settings.TimeZoneId?.Length > 100)
        {
            throw new ArgumentException("Time zone id must not exceed 100 characters.", nameof(settings));
        }

        var normalized = settings with
        {
            WindSpeedUnit = settings.WindSpeedUnit.ToLowerInvariant(),
            WaveHeightUnit = settings.WaveHeightUnit.ToLowerInvariant(),
            TemperatureUnit = settings.TemperatureUnit.ToLowerInvariant(),
            TimeZoneId = NormalizeText(settings.TimeZoneId)
        };
        return _repository.SaveSettingsAsync(RequireUser(userId), normalized, cancellationToken);
    }

    private static Guid RequireUser(Guid userId) => userId != Guid.Empty
        ? userId
        : throw new ArgumentException("A valid user id is required.", nameof(userId));

    private static void ValidateFavorite(SaveFavoriteCommand command)
    {
        if (command.LocationId == Guid.Empty)
        {
            throw new ArgumentException("A valid location id is required.", nameof(command));
        }

        if (command.Note?.Length > 500)
        {
            throw new ArgumentException("Favorite note must not exceed 500 characters.", nameof(command));
        }

        if (command.SortOrder is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Sort order must be between 0 and 10000.");
        }
    }

    private static SaveFavoriteCommand Normalize(SaveFavoriteCommand command) => command with { Note = NormalizeText(command.Note) };

    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
