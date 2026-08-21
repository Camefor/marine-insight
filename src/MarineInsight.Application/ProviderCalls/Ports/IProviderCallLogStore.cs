namespace MarineInsight.Application.ProviderCalls.Ports;

public interface IProviderCallLogStore
{
    Task<Guid> BeginAsync(
        StartProviderCallLog command,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid id,
        CompleteProviderCallLog command,
        CancellationToken cancellationToken = default);

    Task<ProviderCallLogPage> SearchAsync(
        ProviderCallLogFilter filter,
        CancellationToken cancellationToken = default);
}
