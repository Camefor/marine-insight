namespace MarineInsight.Domain.Analysis;

public static class MarineRecommendationWindowPlanner
{
    private const int MinimumWindowHours = 2;
    private const double MinimumConfidence = 0.65;
    private const double FastRiskScoreThreshold = 70;
    private const double FastRiskDropThreshold = 20;
    private static readonly TimeSpan ForecastStep = TimeSpan.FromHours(1);

    public static IReadOnlyList<RecommendationWindow> Plan(
        IEnumerable<HourlyMarineAssessment> hourlyAssessments,
        IEnumerable<ActivityProfile> activityProfiles)
    {
        ArgumentNullException.ThrowIfNull(hourlyAssessments);
        ArgumentNullException.ThrowIfNull(activityProfiles);

        var orderedAssessments = hourlyAssessments
            .OrderBy(assessment => assessment.ForecastTimeUtc)
            .ToArray();
        var profiles = activityProfiles.ToArray();

        return profiles
            .SelectMany(profile => PlanForActivity(orderedAssessments, profile))
            .OrderBy(window => window.StartUtc)
            .ThenBy(window => window.ActivityType)
            .ToArray();
    }

    private static IEnumerable<RecommendationWindow> PlanForActivity(
        HourlyMarineAssessment[] hourlyAssessments,
        ActivityProfile profile)
    {
        var index = 0;
        while (index < hourlyAssessments.Length)
        {
            if (!IsCandidate(hourlyAssessments[index], profile))
            {
                index++;
                continue;
            }

            var startIndex = index;
            while (index + 1 < hourlyAssessments.Length &&
                   IsConsecutive(hourlyAssessments[index], hourlyAssessments[index + 1]) &&
                   IsCandidate(hourlyAssessments[index + 1], profile))
            {
                index++;
            }

            var endIndex = index;
            var durationHours = endIndex - startIndex + 1;
            if (durationHours >= MinimumWindowHours)
            {
                yield return CreateWindow(hourlyAssessments, profile, startIndex, endIndex, durationHours);
            }

            // A single-hour improvement is too volatile for operational advice, so it is
            // deliberately skipped instead of being exposed as a precise recommendation.
            index++;
        }
    }

    private static RecommendationWindow CreateWindow(
        HourlyMarineAssessment[] hourlyAssessments,
        ActivityProfile profile,
        int startIndex,
        int endIndex,
        int durationHours)
    {
        var windowAssessments = hourlyAssessments
            .Skip(startIndex)
            .Take(durationHours)
            .ToArray();
        var bestScore = windowAssessments
            .Select(assessment => GetActivityAssessment(assessment, profile)!.Score!.Value)
            .Max();
        var riskRise = FindRiskRise(hourlyAssessments, profile, endIndex, bestScore);
        DateTimeOffset? returnBeforeUtc = riskRise is null
            ? null
            : riskRise.ForecastTimeUtc - GetReturnBuffer(profile.ActivityType);

        return new RecommendationWindow(
            profile.ActivityType,
            hourlyAssessments[startIndex].ForecastTimeUtc,
            hourlyAssessments[endIndex].ForecastTimeUtc + ForecastStep,
            // 返航截止是按活动类型预留的保守缓冲，不代表航线、船期或现场管理保证。
            returnBeforeUtc,
            riskRise?.ForecastTimeUtc,
            riskRise?.Reason,
            bestScore,
            profile.MinimumRecommendedScore,
            durationHours);
    }

    private static RiskRise? FindRiskRise(
        HourlyMarineAssessment[] hourlyAssessments,
        ActivityProfile profile,
        int endIndex,
        double bestScore)
    {
        var lastWindowTime = hourlyAssessments[endIndex].ForecastTimeUtc;
        return hourlyAssessments
            .Skip(endIndex + 1)
            .TakeWhile(assessment => assessment.ForecastTimeUtc - lastWindowTime <= TimeSpan.FromHours(2))
            .Select(assessment => ToRiskRise(assessment, profile, bestScore))
            .FirstOrDefault(riskRise => riskRise is not null);
    }

    private static RiskRise? ToRiskRise(
        HourlyMarineAssessment assessment,
        ActivityProfile profile,
        double bestScore)
    {
        var activityAssessment = GetActivityAssessment(assessment, profile);
        if (activityAssessment is null)
        {
            return null;
        }

        if (assessment.HasSafetyGate)
        {
            return new RiskRise(assessment.ForecastTimeUtc, GetPrimaryRiskReason(assessment) ?? "安全门禁触发，活动风险快速上升。");
        }

        if (activityAssessment.RiskLevel is RiskLevel.Caution or RiskLevel.Avoid or RiskLevel.Unknown)
        {
            return new RiskRise(assessment.ForecastTimeUtc, GetPrimaryRiskReason(assessment) ?? "活动评分降至高风险等级。");
        }

        if (activityAssessment.Score is { } score &&
            (score < FastRiskScoreThreshold || bestScore - score >= FastRiskDropThreshold))
        {
            return new RiskRise(assessment.ForecastTimeUtc, GetPrimaryRiskReason(assessment) ?? "活动评分在短时间内明显下降。");
        }

        return null;
    }

    private static bool IsCandidate(
        HourlyMarineAssessment assessment,
        ActivityProfile profile)
    {
        var activityAssessment = GetActivityAssessment(assessment, profile);
        return activityAssessment is not null &&
               !assessment.HasSafetyGate &&
               assessment.RiskLevel is not RiskLevel.Avoid and not RiskLevel.Unknown &&
               activityAssessment.RiskLevel is not RiskLevel.Avoid and not RiskLevel.Unknown &&
               activityAssessment.Confidence >= MinimumConfidence &&
               activityAssessment.Score >= profile.MinimumRecommendedScore;
    }

    private static ActivityMarineAssessment? GetActivityAssessment(
        HourlyMarineAssessment assessment,
        ActivityProfile profile) =>
        assessment.ActivityAssessments.FirstOrDefault(activity =>
            activity.ActivityType == profile.ActivityType);

    private static string? GetPrimaryRiskReason(HourlyMarineAssessment assessment) =>
        assessment.Contributions
            .Where(contribution => contribution.Penalty > 0)
            .OrderByDescending(contribution => contribution.Penalty)
            .Select(contribution => contribution.Message)
            .FirstOrDefault();

    private static bool IsConsecutive(
        HourlyMarineAssessment current,
        HourlyMarineAssessment next) =>
        next.ForecastTimeUtc - current.ForecastTimeUtc == ForecastStep;

    private static TimeSpan GetReturnBuffer(ActivityType activityType) => activityType switch
    {
        ActivityType.Boat or ActivityType.Landing => TimeSpan.FromMinutes(60),
        ActivityType.ShoreFishing => TimeSpan.FromMinutes(45),
        ActivityType.Camping or ActivityType.Photography => TimeSpan.FromMinutes(30),
        _ => TimeSpan.FromMinutes(45)
    };

    private sealed record RiskRise(DateTimeOffset ForecastTimeUtc, string Reason);
}
