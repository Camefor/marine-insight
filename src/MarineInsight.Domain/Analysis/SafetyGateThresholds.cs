namespace MarineInsight.Domain.Analysis;

public sealed record SafetyGateThresholds(
    double WindSpeedMs,
    double WindGustMs,
    double WaveHeightM,
    double VisibilityM);
