namespace MarineInsight.Domain.Analysis;

/// <summary>
/// Role a source batch played in an analysis result: the primary input, an
/// enhancement such as tide, a fallback after a primary failure, or a validation set.
/// </summary>
public enum AnalysisSourceRole
{
    Primary,
    Enhancement,
    Fallback,
    Validation
}
