using System.Text.Json.Serialization;

namespace MarineInsight.Application.Analysis;

/// <summary>
/// Raw AI explanation output before fact/safety validation. The provider returns
/// this wire shape; <see cref="ExplanationValidator"/> normalizes it into an
/// <see cref="AnalysisExplanation"/> or rejects it.
/// </summary>
public sealed record ExplanationCandidate
{
    [JsonPropertyName("headline")]
    public string? Headline { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("activityNotes")]
    public IReadOnlyList<ExplanationActivityNote>? ActivityNotes { get; init; }

    [JsonPropertyName("riskWindowText")]
    public string? RiskWindowText { get; init; }

    [JsonPropertyName("uncertaintyText")]
    public string? UncertaintyText { get; init; }

    [JsonPropertyName("disclaimer")]
    public string? Disclaimer { get; init; }
}

public sealed record ExplanationActivityNote
{
    [JsonPropertyName("activity")]
    public string? Activity { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }
}
