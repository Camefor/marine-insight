namespace MarineInsight.Application.Analysis.Ports;

/// <summary>
/// Application port for the optional AI explanation provider. When disabled or when
/// the call fails, callers fall back to the deterministic rule template.
/// </summary>
public interface IExplanationProvider
{
    string ProviderCode { get; }

    string ModelVersion { get; }

    bool IsEnabled { get; }

    Task<ExplanationCandidate> ExplainAsync(
        ExplanationFacts facts,
        CancellationToken cancellationToken);
}
