namespace MarineInsight.Domain.Analysis;

public static class AlgorithmParameterValidator
{
    private static readonly string[] RequiredGoldenSampleIds =
    [
        "GS-001",
        "GS-002",
        "GS-003",
        "GS-004",
        "GS-005",
        "GS-006",
        "GS-007",
        "GS-008",
        "GS-009",
        "GS-010"
    ];

    private static readonly string[] RequiredPenaltyMetrics =
    [
        "windSpeedMs",
        "windGustMs",
        "waveHeightM",
        "swellHeightM",
        "visibilityM",
        "capeJkg"
    ];

    private static readonly string[] RequiredCombinationRules =
    [
        "WIND_LOW_WAVE_HIGH",
        "GUST_VOLATILITY",
        "SHORT_STEEP_WAVE",
        "SWELL_LONG_PERIOD_SHORE",
        "RISK_RISING_FAST"
    ];

    public static AlgorithmParameterValidationResult ValidateForPublication(
        MarineAlgorithmParameters parameters,
        IEnumerable<string> passedGoldenSampleIds)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(passedGoldenSampleIds);

        var issues = new List<AlgorithmParameterValidationIssue>();
        ValidateIdentity(parameters, issues);
        ValidateSafetyGates(parameters.SafetyGates, issues);
        ValidatePenaltyBands(parameters.PenaltyBands, issues);
        ValidateCombinationRules(parameters.CombinationRules, issues);
        ValidateActivityProfiles(parameters.ActivityProfiles, issues);
        ValidateConfidence(parameters.Confidence, issues);
        ValidateRecommendationWindow(parameters.RecommendationWindow, issues);
        ValidateGoldenSamples(passedGoldenSampleIds, issues);

        return new AlgorithmParameterValidationResult(issues);
    }

    private static void ValidateIdentity(
        MarineAlgorithmParameters parameters,
        List<AlgorithmParameterValidationIssue> issues)
    {
        AddIf(string.IsNullOrWhiteSpace(parameters.Version), issues, "ALGORITHM_VERSION_REQUIRED", "Algorithm version is required.");
        AddIf(string.IsNullOrWhiteSpace(parameters.SchemaVersion), issues, "SCHEMA_VERSION_REQUIRED", "Parameter schema version is required.");
        AddIf(
            parameters.SchemaVersion != MarineAlgorithmParameters.CurrentSchemaVersion,
            issues,
            "SCHEMA_VERSION_UNSUPPORTED",
            $"Unsupported parameter schema version: {parameters.SchemaVersion}.");

        var expectedHash = MarineAlgorithmParameters.ComputeConfigurationHash(parameters);
        AddIf(
            string.IsNullOrWhiteSpace(parameters.ConfigurationHash) ||
            !string.Equals(parameters.ConfigurationHash, expectedHash, StringComparison.Ordinal),
            issues,
            "CONFIGURATION_HASH_MISMATCH",
            "Configuration hash must match the canonical parameter payload.");
    }

    private static void ValidateSafetyGates(
        SafetyGateThresholds gates,
        List<AlgorithmParameterValidationIssue> issues)
    {
        ValidatePositive(gates.WindSpeedMs, "SAFETY_GATE_WIND_INVALID", "Wind speed safety gate must be positive.", issues);
        ValidatePositive(gates.WindGustMs, "SAFETY_GATE_GUST_INVALID", "Wind gust safety gate must be positive.", issues);
        ValidatePositive(gates.WaveHeightM, "SAFETY_GATE_WAVE_INVALID", "Wave height safety gate must be positive.", issues);
        ValidatePositive(gates.VisibilityM, "SAFETY_GATE_VISIBILITY_INVALID", "Visibility safety gate must be positive.", issues);
    }

    private static void ValidatePenaltyBands(
        IReadOnlyList<AlgorithmPenaltyBand> bands,
        List<AlgorithmParameterValidationIssue> issues)
    {
        foreach (var metric in RequiredPenaltyMetrics)
        {
            AddIf(
                bands.All(band => band.Metric != metric),
                issues,
                "PENALTY_BAND_REQUIRED",
                $"Penalty bands for {metric} are required.");
        }

        foreach (var band in bands)
        {
            AddIf(string.IsNullOrWhiteSpace(band.Metric), issues, "PENALTY_METRIC_REQUIRED", "Penalty band metric is required.");
            AddIf(!IsFiniteOrNull(band.MinInclusive), issues, "PENALTY_BAND_MIN_INVALID", "Penalty band minimum must be finite when present.");
            AddIf(!IsFiniteOrNull(band.MaxExclusive), issues, "PENALTY_BAND_MAX_INVALID", "Penalty band maximum must be finite when present.");
            AddIf(band.MinInclusive is null && band.MaxExclusive is null, issues, "PENALTY_BAND_BOUND_REQUIRED", "Penalty band must define at least one bound.");
            AddIf(
                band.MinInclusive.HasValue &&
                band.MaxExclusive.HasValue &&
                band.MinInclusive >= band.MaxExclusive,
                issues,
                "PENALTY_BAND_RANGE_INVALID",
                $"Penalty band range for {band.Metric} must have min < max.");
            AddIf(!double.IsFinite(band.Penalty) || band.Penalty < 0, issues, "PENALTY_INVALID", "Penalty must be finite and non-negative.");
        }

        foreach (var group in bands.Where(band => !string.IsNullOrWhiteSpace(band.Metric)).GroupBy(band => band.Metric))
        {
            var ordered = group
                .OrderBy(band => band.MinInclusive ?? double.MinValue)
                .ThenBy(band => band.MaxExclusive ?? double.MaxValue)
                .ToArray();

            for (var index = 1; index < ordered.Length; index++)
            {
                var previousMax = ordered[index - 1].MaxExclusive;
                var currentMin = ordered[index].MinInclusive;
                if (previousMax.HasValue && currentMin.HasValue && currentMin < previousMax)
                {
                    issues.Add(new AlgorithmParameterValidationIssue(
                        "PENALTY_BAND_OVERLAP",
                        $"Penalty bands for {group.Key} must not overlap."));
                }
            }
        }
    }

    private static void ValidateCombinationRules(
        IReadOnlyList<CombinationRuleParameter> rules,
        List<AlgorithmParameterValidationIssue> issues)
    {
        foreach (var code in RequiredCombinationRules)
        {
            AddIf(
                rules.All(rule => rule.Code != code),
                issues,
                "COMBINATION_RULE_REQUIRED",
                $"Combination rule {code} is required.");
        }

        foreach (var rule in rules)
        {
            AddIf(string.IsNullOrWhiteSpace(rule.Code), issues, "COMBINATION_RULE_CODE_REQUIRED", "Combination rule code is required.");
            AddIf(!double.IsFinite(rule.Penalty) || rule.Penalty < 0, issues, "COMBINATION_RULE_PENALTY_INVALID", "Combination rule penalty must be finite and non-negative.");
            AddIf(rule.IsEnabled && rule.Thresholds.Count == 0, issues, "COMBINATION_RULE_THRESHOLD_REQUIRED", $"Enabled rule {rule.Code} must define thresholds.");

            foreach (var threshold in rule.Thresholds)
            {
                AddIf(string.IsNullOrWhiteSpace(threshold.Key), issues, "COMBINATION_RULE_THRESHOLD_NAME_REQUIRED", $"Rule {rule.Code} has an empty threshold name.");
                AddIf(!double.IsFinite(threshold.Value), issues, "COMBINATION_RULE_THRESHOLD_INVALID", $"Rule {rule.Code} threshold {threshold.Key} must be finite.");
            }
        }
    }

    private static void ValidateActivityProfiles(
        IReadOnlyList<ActivityProfile> profiles,
        List<AlgorithmParameterValidationIssue> issues)
    {
        foreach (var activityType in Enum.GetValues<ActivityType>())
        {
            AddIf(
                profiles.All(profile => profile.ActivityType != activityType),
                issues,
                "ACTIVITY_PROFILE_REQUIRED",
                $"Activity profile {activityType} is required.");
        }

        foreach (var group in profiles.GroupBy(profile => profile.ActivityType))
        {
            AddIf(group.Count() > 1, issues, "ACTIVITY_PROFILE_DUPLICATE", $"Activity profile {group.Key} must be unique.");
        }

        foreach (var profile in profiles)
        {
            AddIf(
                !double.IsFinite(profile.MinimumRecommendedScore) ||
                profile.MinimumRecommendedScore is < 0 or > 100,
                issues,
                "ACTIVITY_MINIMUM_SCORE_INVALID",
                $"Activity profile {profile.ActivityType} minimum score must be between 0 and 100.");

            foreach (var dimension in Enum.GetValues<ActivityPenaltyDimension>())
            {
                var multiplier = profile.GetMultiplier(dimension);
                AddIf(
                    !double.IsFinite(multiplier) || multiplier < 0,
                    issues,
                    "ACTIVITY_MULTIPLIER_INVALID",
                    $"Activity profile {profile.ActivityType} multiplier for {dimension} must be finite and non-negative.");
            }
        }
    }

    private static void ValidateConfidence(
        ConfidenceParameters confidence,
        List<AlgorithmParameterValidationIssue> issues)
    {
        ValidateFactor(confidence.StaleFreshnessFactor, "CONFIDENCE_STALE_FRESHNESS_INVALID", issues);
        ValidateFactor(confidence.ExpiredFreshnessFactor, "CONFIDENCE_EXPIRED_FRESHNESS_INVALID", issues);
        ValidateFactor(confidence.PartialStatusFactor, "CONFIDENCE_PARTIAL_STATUS_INVALID", issues);
        ValidateFactor(confidence.StaleStatusFactor, "CONFIDENCE_STALE_STATUS_INVALID", issues);
        ValidateFactor(confidence.InvalidStatusFactor, "CONFIDENCE_INVALID_STATUS_INVALID", issues);
        ValidateFactor(confidence.UnknownStatusFactor, "CONFIDENCE_UNKNOWN_STATUS_INVALID", issues);
        ValidateFactor(confidence.BlockingDataConfidenceCap, "CONFIDENCE_BLOCKING_CAP_INVALID", issues);
        ValidateFactor(confidence.MinimumRecommendationConfidence, "CONFIDENCE_RECOMMENDATION_MINIMUM_INVALID", issues);
    }

    private static void ValidateRecommendationWindow(
        RecommendationWindowParameters window,
        List<AlgorithmParameterValidationIssue> issues)
    {
        AddIf(window.MinimumWindowHours < 2, issues, "WINDOW_MINIMUM_DURATION_INVALID", "Recommendation window duration must be at least two hours.");
        AddIf(
            !double.IsFinite(window.FastRiskScoreThreshold) ||
            window.FastRiskScoreThreshold is < 0 or > 100,
            issues,
            "WINDOW_FAST_RISK_SCORE_INVALID",
            "Fast risk score threshold must be between 0 and 100.");
        AddIf(
            !double.IsFinite(window.FastRiskDropThreshold) ||
            window.FastRiskDropThreshold is <= 0 or > 100,
            issues,
            "WINDOW_FAST_RISK_DROP_INVALID",
            "Fast risk drop threshold must be in the range (0, 100].");

        foreach (var activityType in Enum.GetValues<ActivityType>())
        {
            AddIf(
                !window.ReturnBuffers.TryGetValue(activityType, out var buffer) ||
                buffer <= TimeSpan.Zero,
                issues,
                "WINDOW_RETURN_BUFFER_REQUIRED",
                $"A positive return buffer is required for {activityType}.");
        }
    }

    private static void ValidateGoldenSamples(
        IEnumerable<string> passedGoldenSampleIds,
        List<AlgorithmParameterValidationIssue> issues)
    {
        var passed = passedGoldenSampleIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var goldenSampleId in RequiredGoldenSampleIds)
        {
            AddIf(
                !passed.Contains(goldenSampleId),
                issues,
                "GOLDEN_SAMPLE_REQUIRED",
                $"Golden sample {goldenSampleId} must pass before publishing.");
        }
    }

    private static void ValidatePositive(
        double value,
        string code,
        string message,
        List<AlgorithmParameterValidationIssue> issues) =>
        AddIf(!double.IsFinite(value) || value <= 0, issues, code, message);

    private static void ValidateFactor(
        double value,
        string code,
        List<AlgorithmParameterValidationIssue> issues) =>
        AddIf(!double.IsFinite(value) || value is < 0 or > 1, issues, code, "Confidence factor must be between 0 and 1.");

    private static bool IsFiniteOrNull(double? value) => !value.HasValue || double.IsFinite(value.Value);

    private static void AddIf(
        bool condition,
        List<AlgorithmParameterValidationIssue> issues,
        string code,
        string message)
    {
        if (condition)
        {
            issues.Add(new AlgorithmParameterValidationIssue(code, message));
        }
    }
}
