using MarineInsight.Domain.Analysis;

namespace MarineInsight.Application.Analysis.Ports;

/// <summary>
/// Application boundary for persisted analysis results. Reports are append-only and
/// keyed by the snapshot id of the query that produced them.
/// </summary>
public interface IAnalysisReportRepository
{
    /// <summary>
    /// Persists one analysis report together with its risks and source batch references.
    /// </summary>
    Task SaveAsync(
        AnalysisReport report,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one report by its snapshot id, including risks and source batch references.
    /// </summary>
    Task<AnalysisReport?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the most recent reports owned by a user, newest first.
    /// </summary>
    Task<IReadOnlyList<AnalysisReport>> ListByUserAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);
}
