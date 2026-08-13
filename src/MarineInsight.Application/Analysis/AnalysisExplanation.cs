using MarineInsight.Domain.Analysis;

namespace MarineInsight.Application.Analysis;

/// <summary>
/// Validated explanation returned to the API and Dashboard. When <see cref="Source"/>
/// is <see cref="ExplanationSource.Template"/> the text is deterministic; when
/// <see cref="Source"/> is <see cref="ExplanationSource.Ai"/> it has passed the
/// fact/safety validator. <see cref="Degraded"/> is true when AI was requested but
/// fell back to the template.
/// </summary>
public sealed record AnalysisExplanation(
    ExplanationSource Source,
    bool Degraded,
    string Headline,
    string Summary,
    IReadOnlyList<AnalysisActivityNote> ActivityNotes,
    string? RiskWindowText,
    string? UncertaintyText,
    string Disclaimer,
    string PromptVersion,
    string? ModelVersion,
    string Locale);

public sealed record AnalysisActivityNote(ActivityType Activity, string Text);
