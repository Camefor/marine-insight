namespace MarineInsight.Application.Analysis.Ports;

/// <summary>
/// Cache port for validated AI explanations. Cache failures must remain
/// non-authoritative; a miss simply re-invokes the provider.
/// </summary>
public interface IExplanationCache
{
    Task<AnalysisExplanation?> GetAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        string key,
        AnalysisExplanation explanation,
        ExplanationCachePolicy policy,
        CancellationToken cancellationToken = default);
}
