using MarineInsight.Application.Analysis.Ports;
using MarineInsight.Domain.Analysis;
using Microsoft.Extensions.Logging;

namespace MarineInsight.Application.Analysis;

/// <summary>
/// Orchestrates persistence of analysis results for authenticated users. Ownership
/// is enforced here so a guessed report id can never return another user's report.
/// </summary>
public sealed partial class AnalysisReportService
{
    private readonly IAnalysisReportRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AnalysisReportService> _logger;

    public AnalysisReportService(
        IAnalysisReportRepository repository,
        TimeProvider timeProvider,
        ILogger<AnalysisReportService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AnalysisReport> SaveAsync(
        MarineAnalysisQueryResult result,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var report = AnalysisReportAssembler.FromResult(result, userId, _timeProvider.GetUtcNow());
        await _repository.SaveAsync(report, cancellationToken);
        LogReportSaved(_logger, report.Id, userId);
        return report;
    }

    public async Task<AnalysisReport?> GetByIdAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var report = await _repository.GetByIdAsync(id, cancellationToken);
        return report is not null && report.UserId == userId ? report : null;
    }

    public async Task<IReadOnlyList<AnalysisReport>> ListByUserAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _repository.ListByUserAsync(userId, limit, cancellationToken);
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Analysis report {AnalysisId} saved for user {UserId}.")]
    private static partial void LogReportSaved(ILogger logger, Guid analysisId, Guid userId);
}
