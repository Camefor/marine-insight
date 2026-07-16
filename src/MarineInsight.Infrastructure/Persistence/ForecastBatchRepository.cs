using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Persistence;

public sealed class ForecastBatchRepository(MarineInsightDbContext dbContext) : IForecastBatchRepository
{
    public async Task AppendAsync(
        Guid locationId,
        ForecastBatch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("Location ID is required.", nameof(locationId));
        }

        var entity = ForecastBatchPersistenceMapper.ToEntity(locationId, batch);
        dbContext.ForecastBatches.Add(entity);

        // Add only builds a new aggregate graph; no update path is exposed for immutable forecast data.
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ForecastBatch?> GetByIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        if (batchId == Guid.Empty)
        {
            throw new ArgumentException("Batch ID is required.", nameof(batchId));
        }

        var entity = await QueryWithPoints()
            .SingleOrDefaultAsync(batch => batch.Id == batchId, cancellationToken);

        return entity is null ? null : ForecastBatchPersistenceMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<ForecastBatch>> FindAsync(
        Guid locationId,
        ProviderIdentity provider,
        ForecastDataDomain dataDomain,
        ForecastRange range,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("Location ID is required.", nameof(locationId));
        }

        var dataDomainValue = (short)dataDomain;
        var baseQuery = QueryWithPoints()
            .Where(batch =>
                batch.LocationId == locationId
                && batch.ProviderCode == provider.ProviderCode
                && batch.ModelCode == provider.SourceModel
                && batch.DataDomain == dataDomainValue);

        List<Entities.ForecastBatchEntity> entities;
        if (dbContext.Database.IsSqlite())
        {
            // SQLite maps DateTimeOffset to TEXT and cannot translate ordered comparisons.
            // Keep the indexed identity filter in SQL, then apply the UTC coverage check locally.
            entities = (await baseQuery.ToListAsync(cancellationToken))
                .Where(batch => CoversRange(batch, range))
                .ToList();
        }
        else
        {
            entities = await baseQuery
                .Where(batch =>
                    batch.RangeStartUtc <= range.StartUtc
                    && batch.RangeEndUtc >= range.EndUtc)
                .ToListAsync(cancellationToken);
        }

        entities = entities
            .OrderByDescending(batch => batch.FetchedAtUtc)
            .ThenBy(batch => batch.RangeStartUtc)
            .ToList();

        return entities
            .Select(ForecastBatchPersistenceMapper.ToDomain)
            .ToArray();
    }

    private static bool CoversRange(Entities.ForecastBatchEntity batch, ForecastRange range)
    {
        return batch.RangeStartUtc <= range.StartUtc
            && batch.RangeEndUtc >= range.EndUtc;
    }

    private IQueryable<Entities.ForecastBatchEntity> QueryWithPoints()
    {
        return dbContext.ForecastBatches
            .AsNoTracking()
            .Include(batch => batch.Points)
            .ThenInclude(point => point.Sources);
    }
}
