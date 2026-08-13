using System.Globalization;
using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Analysis;

/// <summary>
/// Deterministic explanation fallback. Always produces a complete, safe summary
/// from the analysis facts without any model call, so P0 queries keep working when
/// AI is disabled or unavailable.
/// </summary>
public static class RuleTemplateExplanationGenerator
{
    public static AnalysisExplanation Generate(ExplanationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return new AnalysisExplanation(
            ExplanationSource.Template,
            false,
            BuildHeadline(facts),
            BuildSummary(facts),
            BuildActivityNotes(facts),
            BuildRiskWindowText(facts),
            BuildUncertaintyText(facts),
            facts.Disclaimer,
            ExplanationDefaults.PromptVersion,
            null,
            ExplanationDefaults.Locale);
    }

    private static string BuildHeadline(ExplanationFacts facts)
    {
        var hasSafetyGate = facts.Risks.Any(risk =>
            risk.Severity is RiskSeverity.Danger or RiskSeverity.Blocking);

        return facts.Overall?.RiskLevel switch
        {
            RiskLevel.VeryGood => hasSafetyGate ? "整体海况平静，仍请注意局部风险。" : "整体海况平静，非常适宜活动。",
            RiskLevel.Good => "整体海况良好，适宜活动。",
            RiskLevel.Moderate => "海况一般，部分时段需注意。",
            RiskLevel.Caution => "海况需谨慎，建议关注风险时段。",
            RiskLevel.Avoid => "存在明显风险，建议避开高风险时段。",
            _ => "当前数据不足，无法可靠判断海况。"
        };
    }

    private static string BuildSummary(ExplanationFacts facts)
    {
        var parts = new List<string>();
        var overall = facts.Overall;

        if (overall?.Score is { } score)
        {
            parts.Add($"综合评分约 {Math.Round(score):0} 分，风险等级为{ToRiskLevelText(overall.RiskLevel)}。");
        }

        var notableRisks = facts.Risks.Take(3).ToArray();
        if (notableRisks.Length > 0)
        {
            parts.Add("主要风险：" + string.Join("；", notableRisks.Select(risk => risk.Message)));
        }

        return parts.Count == 0
            ? "当前数据不足，无法生成可靠海况摘要。"
            : string.Join(" ", parts);
    }

    private static AnalysisActivityNote[] BuildActivityNotes(ExplanationFacts facts) =>
        facts.Activities
            .Select(activity => new AnalysisActivityNote(activity.Activity, BuildActivityNote(activity)))
            .ToArray();

    private static string BuildActivityNote(ExplanationActivityFact activity)
    {
        var label = ToActivityLabel(activity.Activity);
        return activity.RiskLevel switch
        {
            RiskLevel.VeryGood => $"{label}条件理想。",
            RiskLevel.Good => $"{label}基本适宜。",
            RiskLevel.Moderate => $"{label}需留意海况变化。",
            RiskLevel.Caution => $"{label}建议谨慎安排。",
            RiskLevel.Avoid => $"{label}不建议进行。",
            _ => $"{label}数据不足，无法判断。"
        };
    }

    private static string? BuildRiskWindowText(ExplanationFacts facts)
    {
        var returnBefore = facts.RecommendedWindows
            .Where(window => window.ReturnBeforeUtc.HasValue)
            .OrderBy(window => window.ReturnBeforeUtc)
            .FirstOrDefault();
        if (returnBefore is not null)
        {
            return $"预计后续风险上升，建议 {FormatLocalTime(returnBefore.ReturnBeforeUtc!.Value, facts.TimeZoneId)} 前返航。";
        }

        var riskRise = facts.RecommendedWindows
            .Where(window => window.RiskRisesAtUtc.HasValue)
            .OrderBy(window => window.RiskRisesAtUtc)
            .FirstOrDefault();
        if (riskRise is not null)
        {
            return $"预计 {FormatLocalTime(riskRise.RiskRisesAtUtc!.Value, facts.TimeZoneId)} 后风险上升。";
        }

        return null;
    }

    private static string? BuildUncertaintyText(ExplanationFacts facts)
    {
        var reasons = new List<string>();
        if (facts.Overall is { Confidence: < 0.6 })
        {
            reasons.Add("置信度偏低");
        }

        if (facts.QualityStatus != ForecastQualityStatus.Valid)
        {
            reasons.Add("数据质量存在缺口");
        }

        if (facts.MissingMetrics.Count > 0)
        {
            reasons.Add($"部分指标缺失（{string.Join("、", facts.MissingMetrics.Take(3))}）");
        }

        return reasons.Count == 0
            ? null
            : "存在不确定性：" + string.Join("，", reasons) + "。";
    }

    private static string FormatLocalTime(DateTimeOffset utc, string? timeZoneId)
    {
        if (timeZoneId is { Length: > 0 })
        {
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return TimeZoneInfo.ConvertTime(utc, zone).ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return utc.ToString("MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
    }

    private static string ToRiskLevelText(RiskLevel riskLevel) => riskLevel switch
    {
        RiskLevel.VeryGood => "非常适宜",
        RiskLevel.Good => "适宜",
        RiskLevel.Moderate => "一般",
        RiskLevel.Caution => "谨慎",
        RiskLevel.Avoid => "不建议",
        _ => "数据不足"
    };

    private static string ToActivityLabel(ActivityType activityType) => activityType switch
    {
        ActivityType.ShoreFishing => "岸钓",
        ActivityType.Boat => "乘船",
        ActivityType.Landing => "登岛",
        ActivityType.Camping => "露营",
        ActivityType.Photography => "摄影",
        _ => activityType.ToString()
    };
}
