namespace MarineInsight.Application.Users.Ports;

public interface IUserWorkspaceRepository
{
    Task<IReadOnlyList<FavoriteLocation>> ListFavoritesAsync(Guid userId, CancellationToken cancellationToken);

    Task<FavoriteLocation?> AddFavoriteAsync(Guid userId, SaveFavoriteCommand command, CancellationToken cancellationToken);

    Task<FavoriteLocation?> UpdateFavoriteAsync(Guid userId, Guid favoriteId, SaveFavoriteCommand command, CancellationToken cancellationToken);

    Task<bool> DeleteFavoriteAsync(Guid userId, Guid favoriteId, CancellationToken cancellationToken);

    Task<IReadOnlyList<QueryHistoryItem>> ListHistoryAsync(Guid userId, int limit, CancellationToken cancellationToken);

    Task RecordHistoryAsync(Guid userId, RecordQueryHistoryCommand command, CancellationToken cancellationToken);

    Task<UserSettings> GetSettingsAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserSettings> SaveSettingsAsync(Guid userId, UserSettings settings, CancellationToken cancellationToken);
}
