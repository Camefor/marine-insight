using MarineInsight.Application.Analysis.Ports;
using MarineInsight.Domain.Analysis;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Persistence;

public sealed class AnalysisReportRepository(MarineInsightDbContext dbContext) : IAnalysisReportRepository
{
    public async Task SaveAsync(
        AnalysisReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        dbContext.AnalysisResults.Add(AnalysisReportPersistenceMapper.ToEntity(report));

        // Analysis reports are immutable summaries; there is no update path.
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AnalysisReport?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Analysis report ID is required.", nameof(id));
        }

        var entity = await QueryWithChildren()
            .SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

        return entity is null ? null : AnalysisReportPersistenceMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<AnalysisReport>> ListByUserAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        var entities = await QueryWithChildren()
            .Where(report => report.UserId == userId)
            .ToArrayAsync(cancellationToken);

        // SQLite cannot translate DateTimeOffset ordering. The ownership predicate still
        // runs in the database; only the already bounded per-user result is ordered here.
        return entities
            .OrderByDescending(report => report.CreatedAtUtc)
            .Take(limit)
            .Select(AnalysisReportPersistenceMapper.ToDomain)
            .ToArray();
    }

    private IQueryable<Entities.AnalysisReportEntity> QueryWithChildren()
    {
        return dbContext.AnalysisResults
            .AsNoTracking()
            .Include(report => report.Risks)
            .Include(report => report.SourceBatches);
    }
}
