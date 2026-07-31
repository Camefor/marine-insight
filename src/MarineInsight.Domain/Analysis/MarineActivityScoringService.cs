namespace MarineInsight.Domain.Analysis;

public static class MarineActivityScoringService
{
    public static IReadOnlyList<ActivityMarineAssessment> Evaluate(
        HourlyMarineAssessment assessment,
        IEnumerable<ActivityProfile> activityProfiles)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentNullException.ThrowIfNull(activityProfiles);

        return activityProfiles
            .Select(profile => EvaluateActivity(assessment, profile))
            .ToArray();
    }

    private static ActivityMarineAssessment EvaluateActivity(
        HourlyMarineAssessment assessment,
        ActivityProfile profile)
    {
        if (assessment.RiskLevel == RiskLevel.Unknown)
        {
            return new ActivityMarineAssessment(
                profile.ActivityType,
                assessment.ForecastTimeUtc,
                null,
                RiskLevel.Unknown,
                assessment.Confidence,
                assessment.AlgorithmVersion);
        }

        var weightedPenalty = assessment.Contributions.Sum(contribution =>
            contribution.Penalty * GetMultiplier(profile, contribution));
        var rawScore = Math.Clamp(100 - weightedPenalty, 0, 100);
        var score = assessment.HasSafetyGate ? Math.Min(rawScore, 49) : rawScore;

        return new ActivityMarineAssessment(
            profile.ActivityType,
            assessment.ForecastTimeUtc,
            score,
            assessment.HasSafetyGate ? RiskLevel.Avoid : MapRiskLevel(score),
            assessment.Confidence,
            assessment.AlgorithmVersion);
    }

    private static double GetMultiplier(
        ActivityProfile profile,
        RiskContribution contribution)
    {
        if (contribution.Kind == RiskContributionKind.SafetyGate)
        {
            return 1;
        }

        return profile.GetMultiplier(Classify(contribution.Code));
    }

    private static ActivityPenaltyDimension Classify(string code) => code switch
    {
        "WIND_SPEED_BASE" => ActivityPenaltyDimension.Wind,
        "WIND_GUST_BASE" or "GUST_VOLATILITY" => ActivityPenaltyDimension.Gust,
        "WAVE_HEIGHT_BASE" or "WIND_LOW_WAVE_HIGH" => ActivityPenaltyDimension.WaveHeight,
        "SHORT_STEEP_WAVE" or "SHORT_STEEP_WAVE_WATCH" => ActivityPenaltyDimension.ShortPeriodWave,
        "SWELL_HEIGHT_BASE" or "SWELL_LONG_PERIOD_SHORE" => ActivityPenaltyDimension.LongPeriodSwell,
        "VISIBILITY_BASE" => ActivityPenaltyDimension.Visibility,
        "CAPE_BASE" => ActivityPenaltyDimension.RainThunderstorm,
        "DATA_FRESHNESS_DEGRADED" or "DATA_QUALITY_DEGRADED" or "DATA_INSUFFICIENT_MARINE" => ActivityPenaltyDimension.DataQuality,
        _ => ActivityPenaltyDimension.Other
    };

    private static RiskLevel MapRiskLevel(double score) => score switch
    {
        >= 90 => RiskLevel.VeryGood,
        >= 80 => RiskLevel.Good,
        >= 70 => RiskLevel.Moderate,
        >= 50 => RiskLevel.Caution,
        _ => RiskLevel.Avoid
    };
}
