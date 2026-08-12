using MarineInsight.Application.Operations;
using MarineInsight.Application.Operations.Ports;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Persistence;

public sealed class OperationalReadRepository(MarineInsightDbContext dbContext) : IOperationalReadRepository
{
    public async Task<IReadOnlyList<AuditLogItem>> ListAuditLogsAsync(int limit, CancellationToken cancellationToken) =>
        await dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(item => new AuditLogItem(
                item.Id,
                item.ActorUserId,
                item.EventType,
                item.TargetType,
                item.TargetId,
                item.Summary,
                item.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
}
