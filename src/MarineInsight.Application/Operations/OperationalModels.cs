namespace MarineInsight.Application.Operations;

public sealed record ProviderOperationalStatus(
    string ProviderCode,
    string DataDomain,
    bool Enabled,
    bool CredentialConfigured,
    string EndpointHost,
    string Model,
    string Status,
    string Detail);

public sealed record AlgorithmOperationalStatus(
    string Version,
    string SchemaVersion,
    string ConfigurationHash,
    string Status);

public sealed record AuditLogItem(
    Guid Id,
    Guid? ActorUserId,
    string EventType,
    string TargetType,
    string? TargetId,
    string Summary,
    DateTimeOffset CreatedAtUtc);

public sealed record OperationsOverview(
    IReadOnlyList<ProviderOperationalStatus> Providers,
    AlgorithmOperationalStatus Algorithm,
    IReadOnlyList<AuditLogItem> AuditLogs);
