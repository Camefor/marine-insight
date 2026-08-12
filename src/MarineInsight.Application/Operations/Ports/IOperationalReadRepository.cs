namespace MarineInsight.Application.Operations.Ports;

public interface IOperationalReadRepository
{
    Task<IReadOnlyList<AuditLogItem>> ListAuditLogsAsync(int limit, CancellationToken cancellationToken);
}
