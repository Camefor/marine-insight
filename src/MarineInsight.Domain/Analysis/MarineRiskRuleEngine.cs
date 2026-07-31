using MarineInsight.Domain.Forecast;

namespace MarineInsight.Domain.Analysis;

public sealed class MarineRiskRuleEngine
{
    public const string DefaultAlgorithmVersion = "marine-score-1.0.0";

    private const double WindGateMs = 13;
    private const double GustGateMs = 17.2;
    private const double WaveGateM = 2.0;
    private const double VisibilityGateM = 500;
    private readonly string _defaultAlgorithmVersion;

    public MarineRiskRuleEngine(string defaultAlgorithmVersion = DefaultAlgorithmVersion)
    {
        if (string.IsNullOrWhiteSpace(defaultAlgorithmVersion))
        {
            throw new ArgumentException("Default algorithm version is required.", nameof(defaultAlgorithmVersion));
        }

        _defaultAlgorithmVersion = defaultAlgorithmVersion;
    }

    public HourlyMarineAssessment Evaluate(
        ForecastSnapshotPoint point,
        string? algorithmVersion = null)
    {
        ArgumentNullException.ThrowIfNull(point);
        var effectiveAlgorithmVersion = algorithmVersion ?? _defaultAlgorithmVersion;

        var metrics = point.Metrics;
        var contributions = new List<RiskContribution>();
        AddDataQualityContributions(point, contributions);

        if (HasInsufficientMarineData(metrics))
        {
            contributions.Add(new RiskContribution(
                "DATA_INSUFFICIENT_MARINE",
                RiskContributionKind.DataQuality,
                RiskSeverity.Blocking,
                "waveHeightM,swellHeightM",
                null,
                null,
                0,
                "浪高与涌浪同时缺失，无法可靠判断海况风险。"));

            return new HourlyMarineAssessment(
                point.ForecastTimeUtc,
                null,
                RiskLevel.Unknown,
                CalculateConfidence(point.Quality, contributions),
                effectiveAlgorithmVersion,
                contributions);
        }

        AddSafetyGates(metrics, contributions);
        AddBasePenalties(metrics, contributions);
        AddCombinationRules(metrics, contributions);

        var penaltyScore = Math.Clamp(
            100 - contributions.Sum(contribution => contribution.Penalty),
            0,
            100);
        var hasSafetyGate = contributions.Any(contribution => contribution.Kind == RiskContributionKind.SafetyGate);
        var score = hasSafetyGate ? Math.Min(penaltyScore, 49) : penaltyScore;

        return new HourlyMarineAssessment(
            point.ForecastTimeUtc,
            score,
            hasSafetyGate ? RiskLevel.Avoid : MapRiskLevel(score),
            CalculateConfidence(point.Quality, contributions),
            effectiveAlgorithmVersion,
            contributions);
    }

    private static void AddSafetyGates(
        ForecastMetricSet metrics,
        List<RiskContribution> contributions)
    {
        if (metrics.Thunderstorm == true)
        {
            contributions.Add(Gate(
                "THUNDERSTORM_GATE",
                "thunderstorm",
                1,
                1,
                "明确雷暴触发硬性不建议，不能被其他良好指标抵消。"));
        }

        if (metrics.WindSpeedMs >= WindGateMs)
        {
            contributions.Add(Gate(
                "WIND_SPEED_GATE",
                "windSpeedMs",
                metrics.WindSpeedMs,
                WindGateMs,
                "平均风速达到硬性高危阈值。"));
        }

        if (metrics.WindGustMs >= GustGateMs)
        {
            contributions.Add(Gate(
                "WIND_GUST_GATE",
                "windGustMs",
                metrics.WindGustMs,
                GustGateMs,
                "阵风达到硬性高危阈值。"));
        }

        if (metrics.WaveHeightM >= WaveGateM)
        {
            contributions.Add(Gate(
                "WAVE_HEIGHT_GATE",
                "waveHeightM",
                metrics.WaveHeightM,
                WaveGateM,
                "有效波高达到硬性高危阈值。"));
        }

        if (metrics.VisibilityM < VisibilityGateM)
        {
            contributions.Add(Gate(
                "VISIBILITY_LOW_GATE",
                "visibilityM",
                metrics.VisibilityM,
                VisibilityGateM,
                "能见度低于硬性高危阈值。"));
        }
    }

    private static void AddBasePenalties(
        ForecastMetricSet metrics,
        List<RiskContribution> contributions)
    {
        AddPenalty(contributions, "WIND_SPEED_BASE", "windSpeedMs", metrics.WindSpeedMs, WindPenalty(metrics.WindSpeedMs));
        AddPenalty(contributions, "WIND_GUST_BASE", "windGustMs", metrics.WindGustMs, GustPenalty(metrics.WindGustMs));
        AddPenalty(contributions, "WAVE_HEIGHT_BASE", "waveHeightM", metrics.WaveHeightM, WavePenalty(metrics.WaveHeightM));
        AddPenalty(contributions, "SWELL_HEIGHT_BASE", "swellHeightM", metrics.SwellHeightM, SwellPenalty(metrics.SwellHeightM));
        AddPenalty(contributions, "VISIBILITY_BASE", "visibilityM", metrics.VisibilityM, VisibilityPenalty(metrics.VisibilityM));
        AddPenalty(contributions, "CAPE_BASE", "capeJkg", metrics.CapeJkg, CapePenalty(metrics.CapeJkg, metrics.PrecipitationMmPerHour));
    }

    private static void AddCombinationRules(
        ForecastMetricSet metrics,
        List<RiskContribution> contributions)
    {
        if (metrics.WindSpeedMs <= 5 && metrics.WaveHeightM >= 1)
        {
            contributions.Add(new RiskContribution(
                "WIND_LOW_WAVE_HIGH",
                RiskContributionKind.Combination,
                RiskSeverity.Warning,
                "windSpeedMs,waveHeightM",
                metrics.WaveHeightM,
                1,
                10,
                "平均风不高但浪高已经明显，提示远方天气系统或历史海况影响。"));
        }

        if (metrics.WindSpeedMs >= 2 &&
            metrics.WindGustMs >= 9 &&
            metrics.WindGustMs / metrics.WindSpeedMs > 1.5)
        {
            var ratio = metrics.WindGustMs.Value / metrics.WindSpeedMs.Value;
            contributions.Add(new RiskContribution(
                "GUST_VOLATILITY",
                RiskContributionKind.Combination,
                ratio > 2 ? RiskSeverity.Danger : RiskSeverity.Warning,
                "windGustMs/windSpeedMs",
                ratio,
                ratio > 2 ? 2 : 1.5,
                ratio > 2 ? 15 : 8,
                "阵风相对平均风明显偏高，露营、乘船和登岛风险上升。"));
        }

        if (metrics.WavePeriodS < 5 && metrics.WaveHeightM >= 0.8)
        {
            contributions.Add(new RiskContribution(
                "SHORT_STEEP_WAVE",
                RiskContributionKind.Combination,
                RiskSeverity.Warning,
                "wavePeriodS,waveHeightM",
                metrics.WavePeriodS,
                5,
                10,
                "短周期浪更容易造成颠簸和靠泊不稳定。"));
        }
        else if (metrics.WavePeriodS is >= 5 and < 6 && metrics.WaveHeightM >= 0.8)
        {
            contributions.Add(new RiskContribution(
                "SHORT_STEEP_WAVE_WATCH",
                RiskContributionKind.Combination,
                RiskSeverity.Info,
                "wavePeriodS,waveHeightM",
                metrics.WavePeriodS,
                6,
                5,
                "浪周期偏短，需要关注乘船舒适度。"));
        }

        if (metrics.SwellPeriodS >= 12 && metrics.SwellHeightM >= 0.8)
        {
            contributions.Add(new RiskContribution(
                "SWELL_LONG_PERIOD_SHORE",
                RiskContributionKind.Combination,
                RiskSeverity.Danger,
                "swellPeriodS,swellHeightM",
                metrics.SwellPeriodS,
                12,
                15,
                "长周期涌浪可能造成岸边和登岛突然拍浪风险。"));
        }
        else if (metrics.SwellPeriodS >= 10 && metrics.SwellHeightM >= 0.5)
        {
            contributions.Add(new RiskContribution(
                "SWELL_LONG_PERIOD_SHORE",
                RiskContributionKind.Combination,
                RiskSeverity.Warning,
                "swellPeriodS,swellHeightM",
                metrics.SwellPeriodS,
                10,
                8,
                "长周期涌浪不直接代表舒适，岸边活动需额外关注。"));
        }
    }

    private static RiskContribution Gate(
        string code,
        string metric,
        double? actual,
        double threshold,
        string message) => new(
            code,
            RiskContributionKind.SafetyGate,
            RiskSeverity.Blocking,
            metric,
            actual,
            threshold,
            100,
            message);

    private static void AddPenalty(
        List<RiskContribution> contributions,
        string code,
        string metric,
        double? actual,
        double penalty)
    {
        if (penalty <= 0)
        {
            return;
        }

        contributions.Add(new RiskContribution(
            code,
            RiskContributionKind.BasePenalty,
            RiskSeverity.Info,
            metric,
            actual,
            null,
            penalty,
            "基础指标惩罚。"));
    }

    private static double WindPenalty(double? windSpeedMs) => windSpeedMs switch
    {
        null => 0,
        <= 3 => 0,
        <= 5 => 3,
        < 8 => 10,
        < 10 => 20,
        < WindGateMs => 35,
        _ => 0
    };

    private static double GustPenalty(double? gustMs) => gustMs switch
    {
        null => 0,
        <= 6 => 0,
        < 9 => 5,
        < 12 => 12,
        < GustGateMs => 25,
        _ => 0
    };

    private static double WavePenalty(double? waveHeightM) => waveHeightM switch
    {
        null => 0,
        < 0.3 => 0,
        < 0.5 => 3,
        < 1.0 => 12,
        < 1.5 => 25,
        < WaveGateM => 40,
        _ => 0
    };

    private static double SwellPenalty(double? swellHeightM) => swellHeightM switch
    {
        null => 0,
        < 0.5 => 0,
        < 1.0 => 8,
        < 1.5 => 18,
        < 2.0 => 30,
        _ => 40
    };

    private static double VisibilityPenalty(double? visibilityM) => visibilityM switch
    {
        null => 0,
        >= 10_000 => 0,
        >= 5_000 => 3,
        >= 2_000 => 10,
        >= VisibilityGateM => 25,
        _ => 0
    };

    private static double CapePenalty(double? capeJkg, double? precipitationMmPerHour)
    {
        if (capeJkg is null or < 500)
        {
            return 0;
        }

        var hasConvectiveSignal = precipitationMmPerHour >= 1;
        if (capeJkg < 1000)
        {
            return hasConvectiveSignal ? 10 : 5;
        }

        return hasConvectiveSignal ? 20 : 12;
    }

    private static bool HasInsufficientMarineData(ForecastMetricSet metrics) =>
        metrics.WaveHeightM is null && metrics.SwellHeightM is null;

    private static void AddDataQualityContributions(
        ForecastSnapshotPoint point,
        List<RiskContribution> contributions)
    {
        if (point.Quality.Freshness is ForecastFreshness.Stale or ForecastFreshness.Expired)
        {
            contributions.Add(new RiskContribution(
                "DATA_FRESHNESS_DEGRADED",
                RiskContributionKind.DataQuality,
                point.Quality.Freshness == ForecastFreshness.Expired ? RiskSeverity.Danger : RiskSeverity.Warning,
                "freshness",
                null,
                null,
                point.Quality.Freshness == ForecastFreshness.Expired ? 15 : 8,
                "数据时效性下降，评分置信度需要降低。"));
        }

        if (point.Quality.Status is ForecastQualityStatus.Partial or ForecastQualityStatus.Unknown)
        {
            contributions.Add(new RiskContribution(
                "DATA_QUALITY_DEGRADED",
                RiskContributionKind.DataQuality,
                RiskSeverity.Warning,
                "quality",
                point.Quality.Completeness,
                1,
                point.Quality.Status == ForecastQualityStatus.Unknown ? 20 : 8,
                "数据质量不完整，结论置信度需要降低。"));
        }
    }

    private static double CalculateConfidence(
        SnapshotQuality quality,
        IReadOnlyCollection<RiskContribution> contributions)
    {
        var freshnessFactor = quality.Freshness switch
        {
            ForecastFreshness.Fresh => 1,
            ForecastFreshness.Stale => 0.82,
            ForecastFreshness.Expired => 0.55,
            _ => 0.65
        };
        var statusFactor = quality.Status switch
        {
            ForecastQualityStatus.Valid => 1,
            ForecastQualityStatus.Partial => 0.82,
            ForecastQualityStatus.Stale => 0.76,
            ForecastQualityStatus.Invalid => 0.4,
            _ => 0.55
        };
        var blockingDataIssue = contributions.Any(contribution =>
            contribution.Kind == RiskContributionKind.DataQuality &&
            contribution.Severity == RiskSeverity.Blocking);
        var confidence = quality.Completeness * freshnessFactor * statusFactor;

        return Math.Clamp(blockingDataIssue ? Math.Min(confidence, 0.45) : confidence, 0, 1);
    }

    private static RiskLevel MapRiskLevel(double score) => score switch
    {
        >= 90 => RiskLevel.VeryGood,
        >= 80 => RiskLevel.Good,
        >= 70 => RiskLevel.Moderate,
        >= 50 => RiskLevel.Caution,
        _ => RiskLevel.Avoid
    };
}
