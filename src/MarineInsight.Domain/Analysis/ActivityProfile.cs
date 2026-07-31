namespace MarineInsight.Domain.Analysis;

public sealed record ActivityProfile
{
    private readonly IReadOnlyDictionary<ActivityPenaltyDimension, double> _multipliers;

    public ActivityProfile(
        ActivityType activityType,
        double minimumRecommendedScore,
        IReadOnlyDictionary<ActivityPenaltyDimension, double> multipliers)
    {
        if (!double.IsFinite(minimumRecommendedScore) || minimumRecommendedScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumRecommendedScore),
                minimumRecommendedScore,
                "Minimum recommended score must be between 0 and 100.");
        }

        ArgumentNullException.ThrowIfNull(multipliers);
        foreach (var (_, multiplier) in multipliers)
        {
            if (!double.IsFinite(multiplier) || multiplier < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(multipliers), "Activity multipliers must be finite and non-negative.");
            }
        }

        ActivityType = activityType;
        MinimumRecommendedScore = minimumRecommendedScore;
        _multipliers = multipliers;
    }

    public ActivityType ActivityType { get; }

    public double MinimumRecommendedScore { get; }

    public double GetMultiplier(ActivityPenaltyDimension dimension) =>
        _multipliers.TryGetValue(dimension, out var multiplier)
            ? multiplier
            : 1;

    public static IReadOnlyList<ActivityProfile> Defaults { get; } =
    [
        new(ActivityType.ShoreFishing, 70, CreateMultipliers(
            wind: 1.0,
            gust: 1.0,
            waveHeight: 1.3,
            shortPeriodWave: 1.0,
            longPeriodSwell: 1.5,
            visibility: 0.6,
            rainThunderstorm: 1.0)),
        new(ActivityType.Boat, 75, CreateMultipliers(
            wind: 1.2,
            gust: 1.2,
            waveHeight: 1.3,
            shortPeriodWave: 1.4,
            longPeriodSwell: 1.0,
            visibility: 1.4,
            rainThunderstorm: 1.0)),
        new(ActivityType.Landing, 75, CreateMultipliers(
            wind: 1.1,
            gust: 1.2,
            waveHeight: 1.5,
            shortPeriodWave: 1.3,
            longPeriodSwell: 1.5,
            visibility: 1.1,
            rainThunderstorm: 1.0)),
        new(ActivityType.Camping, 70, CreateMultipliers(
            wind: 0.8,
            gust: 1.4,
            waveHeight: 0.3,
            shortPeriodWave: 0.0,
            longPeriodSwell: 0.2,
            visibility: 0.2,
            rainThunderstorm: 1.4)),
        new(ActivityType.Photography, 70, CreateMultipliers(
            wind: 0.6,
            gust: 0.7,
            waveHeight: 0.5,
            shortPeriodWave: 0.2,
            longPeriodSwell: 0.5,
            visibility: 1.2,
            rainThunderstorm: 1.2))
    ];

    public static IReadOnlyList<ActivityProfile> SelectDefaults(IEnumerable<ActivityType>? activityTypes)
    {
        if (activityTypes is null)
        {
            return Defaults;
        }

        var requested = activityTypes.Distinct().ToArray();
        return requested.Length == 0
            ? Defaults
            : Defaults.Where(profile => requested.Contains(profile.ActivityType)).ToArray();
    }

    private static Dictionary<ActivityPenaltyDimension, double> CreateMultipliers(
        double wind,
        double gust,
        double waveHeight,
        double shortPeriodWave,
        double longPeriodSwell,
        double visibility,
        double rainThunderstorm) =>
        new Dictionary<ActivityPenaltyDimension, double>
        {
            [ActivityPenaltyDimension.Wind] = wind,
            [ActivityPenaltyDimension.Gust] = gust,
            [ActivityPenaltyDimension.WaveHeight] = waveHeight,
            [ActivityPenaltyDimension.ShortPeriodWave] = shortPeriodWave,
            // 长周期涌浪对岸边和登岛更敏感，不能按“周期长更舒适”统一降权。
            [ActivityPenaltyDimension.LongPeriodSwell] = longPeriodSwell,
            [ActivityPenaltyDimension.Visibility] = visibility,
            [ActivityPenaltyDimension.RainThunderstorm] = rainThunderstorm,
            [ActivityPenaltyDimension.DataQuality] = 1,
            [ActivityPenaltyDimension.Other] = 1
        };
}
