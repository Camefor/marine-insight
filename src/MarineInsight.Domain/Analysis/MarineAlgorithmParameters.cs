using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MarineInsight.Domain.Analysis;

public sealed record MarineAlgorithmParameters
{
    public const string CurrentSchemaVersion = "marine-score-parameters.v1";

    public MarineAlgorithmParameters(
        string version,
        string schemaVersion,
        SafetyGateThresholds safetyGates,
        IEnumerable<AlgorithmPenaltyBand> penaltyBands,
        IEnumerable<CombinationRuleParameter> combinationRules,
        IEnumerable<ActivityProfile> activityProfiles,
        ConfidenceParameters confidence,
        RecommendationWindowParameters recommendationWindow,
        string? configurationHash = null)
    {
        ArgumentNullException.ThrowIfNull(safetyGates);
        ArgumentNullException.ThrowIfNull(penaltyBands);
        ArgumentNullException.ThrowIfNull(combinationRules);
        ArgumentNullException.ThrowIfNull(activityProfiles);
        ArgumentNullException.ThrowIfNull(confidence);
        ArgumentNullException.ThrowIfNull(recommendationWindow);

        Version = version;
        SchemaVersion = schemaVersion;
        SafetyGates = safetyGates;
        PenaltyBands = Array.AsReadOnly(penaltyBands.ToArray());
        CombinationRules = Array.AsReadOnly(combinationRules.ToArray());
        ActivityProfiles = Array.AsReadOnly(activityProfiles.ToArray());
        Confidence = confidence;
        RecommendationWindow = recommendationWindow;
        ConfigurationHash = configurationHash ?? ComputeConfigurationHash(this);
    }

    public string Version { get; }

    public string SchemaVersion { get; }

    public SafetyGateThresholds SafetyGates { get; }

    public IReadOnlyList<AlgorithmPenaltyBand> PenaltyBands { get; }

    public IReadOnlyList<CombinationRuleParameter> CombinationRules { get; }

    public IReadOnlyList<ActivityProfile> ActivityProfiles { get; }

    public ConfidenceParameters Confidence { get; }

    public RecommendationWindowParameters RecommendationWindow { get; }

    public string ConfigurationHash { get; }

    public MarineAlgorithmParameters WithConfigurationHash(string configurationHash) => new(
        Version,
        SchemaVersion,
        SafetyGates,
        PenaltyBands,
        CombinationRules,
        ActivityProfiles,
        Confidence,
        RecommendationWindow,
        configurationHash);

    public static MarineAlgorithmParameters CreateDefault() => new(
        MarineRiskRuleEngine.DefaultAlgorithmVersion,
        CurrentSchemaVersion,
        new SafetyGateThresholds(
            WindSpeedMs: 13,
            WindGustMs: 18,
            WaveHeightM: 2.0,
            VisibilityM: 500),
        CreateDefaultPenaltyBands(),
        CreateDefaultCombinationRules(),
        ActivityProfile.Defaults,
        new ConfidenceParameters(
            StaleFreshnessFactor: 0.82,
            ExpiredFreshnessFactor: 0.55,
            PartialStatusFactor: 0.82,
            StaleStatusFactor: 0.76,
            InvalidStatusFactor: 0.4,
            UnknownStatusFactor: 0.55,
            BlockingDataConfidenceCap: 0.45,
            MinimumRecommendationConfidence: 0.65),
        new RecommendationWindowParameters(
            minimumWindowHours: 2,
            fastRiskScoreThreshold: 70,
            fastRiskDropThreshold: 20,
            new Dictionary<ActivityType, TimeSpan>
            {
                [ActivityType.Boat] = TimeSpan.FromMinutes(60),
                [ActivityType.Landing] = TimeSpan.FromMinutes(60),
                [ActivityType.ShoreFishing] = TimeSpan.FromMinutes(45),
                [ActivityType.Camping] = TimeSpan.FromMinutes(30),
                [ActivityType.Photography] = TimeSpan.FromMinutes(30)
            }));

    public static string ComputeConfigurationHash(MarineAlgorithmParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var canonical = BuildCanonicalText(parameters);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));

        return $"sha256:{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private static IReadOnlyList<AlgorithmPenaltyBand> CreateDefaultPenaltyBands() =>
    [
        new("windSpeedMs", null, 3, 0),
        new("windSpeedMs", 3, 5, 3),
        new("windSpeedMs", 5, 8, 10),
        new("windSpeedMs", 8, 10, 20),
        new("windSpeedMs", 10, 13, 35),
        new("windGustMs", null, 6, 0),
        new("windGustMs", 6, 9, 5),
        new("windGustMs", 9, 12, 12),
        new("windGustMs", 12, 18, 25),
        new("waveHeightM", null, 0.3, 0),
        new("waveHeightM", 0.3, 0.5, 3),
        new("waveHeightM", 0.5, 1.0, 12),
        new("waveHeightM", 1.0, 1.5, 25),
        new("waveHeightM", 1.5, 2.0, 40),
        new("swellHeightM", null, 0.5, 0),
        new("swellHeightM", 0.5, 1.0, 8),
        new("swellHeightM", 1.0, 1.5, 18),
        new("swellHeightM", 1.5, 2.0, 30),
        new("visibilityM", 10_000, null, 0),
        new("visibilityM", 5_000, 10_000, 3),
        new("visibilityM", 2_000, 5_000, 10),
        new("visibilityM", 500, 2_000, 25),
        new("capeJkg", 500, 1_000, 5),
        new("capeJkg", 1_000, null, 12)
    ];

    private static IReadOnlyList<CombinationRuleParameter> CreateDefaultCombinationRules() =>
    [
        new("WIND_LOW_WAVE_HIGH", 10, new Dictionary<string, double>
        {
            ["windSpeedMaxMs"] = 5,
            ["waveHeightMinM"] = 1
        }),
        new("GUST_VOLATILITY", 15, new Dictionary<string, double>
        {
            ["windSpeedMinMs"] = 2,
            ["gustMinMs"] = 9,
            ["gustRatioDanger"] = 2
        }),
        new("SHORT_STEEP_WAVE", 10, new Dictionary<string, double>
        {
            ["waveHeightMinM"] = 0.8,
            ["wavePeriodMaxS"] = 5
        }),
        new("SHORT_STEEP_WAVE_WATCH", 5, new Dictionary<string, double>
        {
            ["waveHeightMinM"] = 0.8,
            ["wavePeriodWatchMaxS"] = 6
        }),
        new("SWELL_LONG_PERIOD_SHORE", 15, new Dictionary<string, double>
        {
            ["swellHeightMinM"] = 0.8,
            ["swellPeriodDangerMinS"] = 12
        }),
        new("RISK_RISING_FAST", 10, new Dictionary<string, double>
        {
            ["lookAheadHours"] = 2,
            ["scoreDropMin"] = 20
        })
    ];

    private static string BuildCanonicalText(MarineAlgorithmParameters parameters)
    {
        var builder = new StringBuilder();
        builder.AppendLine(parameters.Version);
        builder.AppendLine(parameters.SchemaVersion);
        builder.AppendLine(CultureInfo.InvariantCulture, $"gate:{parameters.SafetyGates.WindSpeedMs}:{parameters.SafetyGates.WindGustMs}:{parameters.SafetyGates.WaveHeightM}:{parameters.SafetyGates.VisibilityM}");

        foreach (var band in parameters.PenaltyBands
                     .OrderBy(band => band.Metric, StringComparer.Ordinal)
                     .ThenBy(band => band.MinInclusive ?? double.MinValue)
                     .ThenBy(band => band.MaxExclusive ?? double.MaxValue))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"band:{band.Metric}:{band.MinInclusive}:{band.MaxExclusive}:{band.Penalty}");
        }

        foreach (var rule in parameters.CombinationRules.OrderBy(rule => rule.Code, StringComparer.Ordinal))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"rule:{rule.Code}:{rule.Penalty}:{rule.IsEnabled}");
            foreach (var threshold in rule.Thresholds.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"rule-threshold:{threshold.Key}:{threshold.Value}");
            }
        }

        foreach (var profile in parameters.ActivityProfiles.OrderBy(profile => profile.ActivityType))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"activity:{profile.ActivityType}:{profile.MinimumRecommendedScore}");
            foreach (var dimension in Enum.GetValues<ActivityPenaltyDimension>())
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"activity-multiplier:{dimension}:{profile.GetMultiplier(dimension)}");
            }
        }

        var confidence = parameters.Confidence;
        builder.AppendLine(CultureInfo.InvariantCulture, $"confidence:{confidence.StaleFreshnessFactor}:{confidence.ExpiredFreshnessFactor}:{confidence.PartialStatusFactor}:{confidence.StaleStatusFactor}:{confidence.InvalidStatusFactor}:{confidence.UnknownStatusFactor}:{confidence.BlockingDataConfidenceCap}:{confidence.MinimumRecommendationConfidence}");

        var window = parameters.RecommendationWindow;
        builder.AppendLine(CultureInfo.InvariantCulture, $"window:{window.MinimumWindowHours}:{window.FastRiskScoreThreshold}:{window.FastRiskDropThreshold}");
        foreach (var buffer in window.ReturnBuffers.OrderBy(pair => pair.Key))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"buffer:{buffer.Key}:{buffer.Value.TotalMinutes}");
        }

        return builder.ToString();
    }
}
