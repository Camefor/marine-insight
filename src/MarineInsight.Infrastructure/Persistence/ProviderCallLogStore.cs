using MarineInsight.Application.ProviderCalls;
using MarineInsight.Application.ProviderCalls.Ports;
using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Persistence;

public sealed class ProviderCallLogStore(
    MarineInsightDbContext dbContext,
    TimeProvider timeProvider) : IProviderCallLogStore
{
    public async Task<Guid> BeginAsync(
        StartProviderCallLog command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var entity = new ProviderCallLogEntity
        {
            Id = Guid.NewGuid(),
            ActorUserId = command.ActorUserId,
            ProviderCode = command.ProviderCode,
            Operation = command.Operation,
            CredentialId = command.CredentialId,
            CredentialHint = command.CredentialHint,
            LatitudeBucket = command.LatitudeBucket,
            LongitudeBucket = command.LongitudeBucket,
            RangeStartUtc = command.RangeStartUtc,
            RangeEndUtc = command.RangeEndUtc,
            RequestedDays = command.RequestedDays,
            Outcome = ProviderCallOutcomes.Started,
            TraceId = command.TraceId,
            StartedAtUtc = timeProvider.GetUtcNow()
        };
        dbContext.ProviderCallLogs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task CompleteAsync(
        Guid id,
        CompleteProviderCallLog command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var entity = await dbContext.ProviderCallLogs.SingleAsync(item => item.Id == id, cancellationToken);
        entity.Outcome = command.Succeeded
            ? ProviderCallOutcomes.Succeeded
            : ProviderCallOutcomes.Failed;
        entity.HttpStatusCode = command.HttpStatusCode;
        entity.CreditsUsed = command.CreditsUsed;
        entity.RemainingCredits = command.RemainingCredits;
        entity.DurationMs = command.DurationMs;
        entity.ErrorCode = command.ErrorCode;
        entity.CompletedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProviderCallLogPage> SearchAsync(
        ProviderCallLogFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ProviderCallLogs.AsNoTracking();
        if (filter.ProviderCode is not null)
        {
            query = query.Where(item => item.ProviderCode == filter.ProviderCode);
        }

        if (filter.Operation is not null)
        {
            query = query.Where(item => item.Operation == filter.Operation);
        }

        if (filter.Outcome is not null)
        {
            query = query.Where(item => item.Outcome == filter.Outcome);
        }

        if (filter.ActorUserId.HasValue)
        {
            query = query.Where(item => item.ActorUserId == filter.ActorUserId.Value);
        }

        if (dbContext.Database.IsSqlite())
        {
            // SQLite cannot translate DateTimeOffset ordering/range predicates. This table is
            // operational data with bounded admin paging, so local/test mode filters in memory.
            var candidates = await query.ToArrayAsync(cancellationToken);
            return ToPage(candidates, filter);
        }

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(item => item.StartedAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(item => item.StartedAtUtc <= filter.ToUtc.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(item => item.StartedAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToArrayAsync(cancellationToken);
        return new ProviderCallLogPage(entities.Select(Project).ToArray(), total, filter.Page, filter.PageSize);
    }

    private static ProviderCallLogPage ToPage(
        IEnumerable<ProviderCallLogEntity> candidates,
        ProviderCallLogFilter filter)
    {
        var filtered = candidates
            .Where(item => !filter.FromUtc.HasValue || item.StartedAtUtc >= filter.FromUtc.Value)
            .Where(item => !filter.ToUtc.HasValue || item.StartedAtUtc <= filter.ToUtc.Value)
            .OrderByDescending(item => item.StartedAtUtc)
            .ThenByDescending(item => item.Id)
            .ToArray();
        var page = filtered
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(Project)
            .ToArray();
        return new ProviderCallLogPage(page, filtered.Length, filter.Page, filter.PageSize);
    }

    private static ProviderCallLogItem Project(ProviderCallLogEntity entity) => new(
        entity.Id,
        entity.ActorUserId,
        entity.ProviderCode,
        entity.Operation,
        entity.CredentialId,
        entity.CredentialHint,
        entity.LatitudeBucket,
        entity.LongitudeBucket,
        entity.RangeStartUtc,
        entity.RangeEndUtc,
        entity.RequestedDays,
        entity.Outcome,
        entity.HttpStatusCode,
        entity.CreditsUsed,
        entity.RemainingCredits,
        entity.DurationMs,
        entity.ErrorCode,
        entity.TraceId,
        entity.StartedAtUtc,
        entity.CompletedAtUtc);
}
