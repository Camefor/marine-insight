using System.Globalization;
using System.Text.RegularExpressions;
using MarineInsight.Domain.Analysis;

namespace MarineInsight.Application.Analysis;

/// <summary>
/// Validates raw AI output against the deterministic facts before it can replace
/// the rule template. Any failure discards the AI text entirely: a wrong template
/// is safer than a confident hallucination.
/// </summary>
public static partial class ExplanationValidator
{
    private const double NumericTolerance = 0.05;

    private static readonly string[] OptimisticTerms =
    [
        "非常适宜", "适宜", "安全", "无忧", "平静", "良好", "放心",
        "无风险", "风平浪静", "理想", "很适合", "放心出海"
    ];

    private static readonly string[] CautionTerms =
    [
        "谨慎", "注意", "风险", "避免", "不建议", "不宜", "小心",
        "警惕", "返航", "远离", "危险", "禁行", "禁止"
    ];

    private static readonly string[] ForbiddenWhenClearTerms = ["禁行", "禁止", "危险", "切勿", "严禁"];

    public static AnalysisExplanation? TryValidate(
        ExplanationCandidate candidate,
        ExplanationFacts facts,
        string modelVersion)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(modelVersion);

        var headline = candidate.Headline?.Trim();
        var summary = candidate.Summary?.Trim();
        if (string.IsNullOrWhiteSpace(headline) || headline.Length > ExplanationDefaults.MaxHeadlineLength)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(summary) || summary.Length > ExplanationDefaults.MaxSummaryLength)
        {
            return null;
        }

        var notes = ParseActivityNotes(candidate, facts);
        if (notes is null)
        {
            return null;
        }

        var riskWindowText = NormalizeOptional(candidate.RiskWindowText, ExplanationDefaults.MaxRiskWindowTextLength, out var riskWindowValid);
        if (!riskWindowValid)
        {
            return null;
        }

        var uncertaintyText = NormalizeOptional(candidate.UncertaintyText, ExplanationDefaults.MaxUncertaintyTextLength, out var uncertaintyValid);
        if (!uncertaintyValid)
        {
            return null;
        }

        var combinedText = string.Join(
            " ",
            new[] { headline, summary }
                .Concat(notes.Select(note => note.Text))
                .Append(riskWindowText)
                .Append(uncertaintyText)
                .Where(text => !string.IsNullOrWhiteSpace(text)));

        if (!IsRiskConsistent(combinedText, facts))
        {
            return null;
        }

        if (!IsNumericWhitelistSatisfied(combinedText, facts))
        {
            return null;
        }

        return new AnalysisExplanation(
            ExplanationSource.Ai,
            false,
            headline!,
            summary!,
            notes,
            riskWindowText,
            uncertaintyText,
            facts.Disclaimer,
            ExplanationDefaults.PromptVersion,
            modelVersion,
            ExplanationDefaults.Locale);
    }

    private static List<AnalysisActivityNote>? ParseActivityNotes(
        ExplanationCandidate candidate,
        ExplanationFacts facts)
    {
        var rawNotes = candidate.ActivityNotes ?? [];
        var notes = new List<AnalysisActivityNote>(rawNotes.Count);
        foreach (var raw in rawNotes)
        {
            var activity = ParseActivity(raw.Activity);
            var text = raw.Text?.Trim();
            if (activity is null || string.IsNullOrWhiteSpace(text) || text.Length > ExplanationDefaults.MaxActivityNoteLength)
            {
                return null;
            }

            // Activity consistency: the AI may only describe activities the user requested.
            if (!facts.SupportedActivities.Contains(activity.Value))
            {
                return null;
            }

            notes.Add(new AnalysisActivityNote(activity.Value, text));
        }

        return notes;
    }

    private static bool IsRiskConsistent(string text, ExplanationFacts facts)
    {
        var mustWarn = facts.Risks.Any(risk => risk.Severity is RiskSeverity.Danger or RiskSeverity.Blocking)
            || facts.Overall?.RiskLevel is RiskLevel.Caution or RiskLevel.Avoid
            || facts.Activities.Any(activity => activity.RiskLevel is RiskLevel.Caution or RiskLevel.Avoid);

        if (mustWarn)
        {
            return !OptimisticTerms.Any(text.Contains) && CautionTerms.Any(text.Contains);
        }

        return !ForbiddenWhenClearTerms.Any(text.Contains);
    }

    private static bool IsNumericWhitelistSatisfied(string text, ExplanationFacts facts)
    {
        var allowed = CollectFactNumbers(facts);
        foreach (Match match in NumberRegex().Matches(text))
        {
            if (!double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                continue;
            }

            // Small integers are treated as hour/count prose ("2-3 个原因", "12 秒涌浪").
            if (number >= 0 && number <= 24 && Math.Abs(number - Math.Round(number)) < 0.001)
            {
                continue;
            }

            if (!allowed.Any(fact => Math.Abs(number - fact) <= NumericTolerance))
            {
                return false;
            }
        }

        return true;
    }

    private static double[] CollectFactNumbers(ExplanationFacts facts)
    {
        var values = new List<double> { facts.Hours };
        var overall = facts.Overall;
        if (overall is not null)
        {
            AddIfPresent(values, overall.Score);
            AddIfPresent(values, overall.Confidence);
            AddIfPresent(values, Math.Round(overall.Confidence * 100));
        }

        AddIfPresent(values, facts.Completeness);
        AddIfPresent(values, Math.Round(facts.Completeness * 100));

        foreach (var activity in facts.Activities)
        {
            AddIfPresent(values, activity.Score);
        }

        foreach (var risk in facts.Risks)
        {
            AddIfPresent(values, risk.Actual);
            AddIfPresent(values, risk.Threshold);
            AddIfPresent(values, risk.Penalty);
        }

        return values.ToArray();
    }

    private static void AddIfPresent(List<double> values, double? value)
    {
        if (value.HasValue && double.IsFinite(value.Value))
        {
            values.Add(value.Value);
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength, out bool valid)
    {
        var trimmed = value?.Trim();
        if (trimmed is null || trimmed.Length == 0)
        {
            valid = true;
            return null;
        }

        valid = trimmed.Length <= maxLength;
        return valid ? trimmed : null;
    }

    private static ActivityType? ParseActivity(string? value) => value switch
    {
        "shoreFishing" => ActivityType.ShoreFishing,
        "boat" => ActivityType.Boat,
        "landing" => ActivityType.Landing,
        "camping" => ActivityType.Camping,
        "photography" => ActivityType.Photography,
        _ => null
    };

    [GeneratedRegex(@"\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();
}
