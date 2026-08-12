using MarineInsight.Application.Users;
using MarineInsight.Application.Users.Ports;
using MarineInsight.Domain.Analysis;
using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Persistence;

public sealed class UserWorkspaceRepository(
    MarineInsightDbContext dbContext,
    TimeProvider timeProvider) : IUserWorkspaceRepository
{
    public async Task<IReadOnlyList<FavoriteLocation>> ListFavoritesAsync(Guid userId, CancellationToken cancellationToken)
    {
        // User ownership is part of every database predicate so a guessed resource id
        // can never widen the query before authorization is applied.
        var entities = await dbContext.FavoriteLocations
            .AsNoTracking()
            .Include(entity => entity.Location)
            .Where(entity => entity.UserId == userId)
            .OrderBy(entity => entity.SortOrder)
            .ThenBy(entity => entity.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);
        return entities.Select(MapFavorite).ToArray();
    }

    public async Task<FavoriteLocation?> AddFavoriteAsync(Guid userId, SaveFavoriteCommand command, CancellationToken cancellationToken)
    {
        if (await dbContext.FavoriteLocations.AnyAsync(
            entity => entity.UserId == userId && entity.LocationId == command.LocationId,
            cancellationToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var entity = new FavoriteLocationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LocationId = command.LocationId,
            DefaultActivity = command.DefaultActivity?.ToString(),
            Note = command.Note,
            SortOrder = command.SortOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.FavoriteLocations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(entity).Reference(item => item.Location).LoadAsync(cancellationToken);
        return MapFavorite(entity);
    }

    public async Task<FavoriteLocation?> UpdateFavoriteAsync(Guid userId, Guid favoriteId, SaveFavoriteCommand command, CancellationToken cancellationToken)
    {
        var entity = await dbContext.FavoriteLocations
            .Include(item => item.Location)
            .SingleOrDefaultAsync(item => item.Id == favoriteId && item.UserId == userId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.DefaultActivity = command.DefaultActivity?.ToString();
        entity.Note = command.Note;
        entity.SortOrder = command.SortOrder;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapFavorite(entity);
    }

    public async Task<bool> DeleteFavoriteAsync(Guid userId, Guid favoriteId, CancellationToken cancellationToken)
    {
        var affected = await dbContext.FavoriteLocations
            .Where(entity => entity.Id == favoriteId && entity.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        return affected == 1;
    }

    public async Task<IReadOnlyList<QueryHistoryItem>> ListHistoryAsync(Guid userId, int limit, CancellationToken cancellationToken)
    {
        var entities = await dbContext.QueryHistory
            .AsNoTracking()
            .Where(entity => entity.UserId == userId)
            .OrderByDescending(entity => entity.CreatedAtUtc)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        return entities.Select(MapHistory).ToArray();
    }

    public async Task RecordHistoryAsync(Guid userId, RecordQueryHistoryCommand command, CancellationToken cancellationToken)
    {
        dbContext.QueryHistory.Add(new QueryHistoryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LocationId = command.LocationId,
            DisplayName = command.DisplayName,
            Latitude = command.Latitude,
            Longitude = command.Longitude,
            ForecastFromUtc = command.ForecastFromUtc,
            Hours = command.Hours,
            Activities = string.Join(',', command.Activities.Select(activity => activity.ToString())),
            AnalysisId = command.AnalysisId,
            RiskLevel = command.RiskLevel,
            Score = command.Score,
            CreatedAtUtc = timeProvider.GetUtcNow()
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserSettings> GetSettingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.UserSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return entity is null ? UserSettings.Default : MapSettings(entity);
    }

    public async Task<UserSettings> SaveSettingsAsync(Guid userId, UserSettings settings, CancellationToken cancellationToken)
    {
        var entity = await dbContext.UserSettings.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (entity is null)
        {
            entity = new UserSettingEntity { UserId = userId };
            dbContext.UserSettings.Add(entity);
        }

        entity.WindSpeedUnit = settings.WindSpeedUnit;
        entity.WaveHeightUnit = settings.WaveHeightUnit;
        entity.TemperatureUnit = settings.TemperatureUnit;
        entity.DefaultActivity = settings.DefaultActivity?.ToString();
        entity.TimeZoneId = settings.TimeZoneId;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapSettings(entity);
    }

    private static FavoriteLocation MapFavorite(FavoriteLocationEntity entity) => new(
        entity.Id,
        entity.LocationId,
        entity.Location.DisplayName,
        (double)entity.Location.Latitude,
        (double)entity.Location.Longitude,
        ParseActivity(entity.DefaultActivity),
        entity.Note,
        entity.SortOrder,
        entity.CreatedAtUtc);

    private static QueryHistoryItem MapHistory(QueryHistoryEntity entity) => new(
        entity.Id,
        entity.LocationId,
        entity.DisplayName,
        entity.Latitude,
        entity.Longitude,
        entity.ForecastFromUtc,
        entity.Hours,
        entity.Activities.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseActivity)
            .Where(activity => activity.HasValue)
            .Select(activity => activity!.Value)
            .ToArray(),
        entity.AnalysisId,
        entity.RiskLevel,
        entity.Score,
        entity.CreatedAtUtc);

    private static UserSettings MapSettings(UserSettingEntity entity) => new(
        entity.WindSpeedUnit,
        entity.WaveHeightUnit,
        entity.TemperatureUnit,
        ParseActivity(entity.DefaultActivity),
        entity.TimeZoneId);

    private static ActivityType? ParseActivity(string? value) =>
        Enum.TryParse<ActivityType>(value, ignoreCase: true, out var activity) ? activity : null;
}
