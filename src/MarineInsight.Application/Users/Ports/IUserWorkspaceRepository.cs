namespace MarineInsight.Application.Users.Ports;

public interface IUserWorkspaceRepository
{
    Task<IReadOnlyList<FavoriteLocation>> ListFavoritesAsync(Guid userId, CancellationToken cancellationToken);

    Task<FavoriteLocation?> AddFavoriteAsync(Guid userId, SaveFavoriteCommand command, CancellationToken cancellationToken);

    Task<FavoriteLocation?> UpdateFavoriteAsync(Guid userId, Guid favoriteId, SaveFavoriteCommand command, CancellationToken cancellationToken);

    Task<bool> DeleteFavoriteAsync(Guid userId, Guid favoriteId, CancellationToken cancellationToken);

    Task<IReadOnlyList<QueryHistoryItem>> ListHistoryAsync(Guid userId, int limit, CancellationToken cancellationToken);

    Task RecordHistoryAsync(Guid userId, RecordQueryHistoryCommand command, CancellationToken cancellationToken);

    Task<bool> DeleteHistoryAsync(Guid userId, Guid historyId, CancellationToken cancellationToken);

    Task<int> ClearHistoryAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserLocation>> ListUserLocationsAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserLocation> AddUserLocationAsync(Guid userId, SaveUserLocationCommand command, CancellationToken cancellationToken);

    Task<UserLocation?> UpdateUserLocationAsync(Guid userId, Guid userLocationId, SaveUserLocationCommand command, CancellationToken cancellationToken);

    Task<bool> DeleteUserLocationAsync(Guid userId, Guid userLocationId, CancellationToken cancellationToken);

    Task<UserSettings> GetSettingsAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserSettings> SaveSettingsAsync(Guid userId, UserSettings settings, CancellationToken cancellationToken);
}
