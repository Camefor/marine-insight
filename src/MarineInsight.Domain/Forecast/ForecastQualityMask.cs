namespace MarineInsight.Domain.Forecast;

[Flags]
public enum ForecastQualityMask
{
    None = 0,
    MissingMetric = 1 << 0,
    StaleData = 1 << 1,
    ExpiredData = 1 << 2,
    TimeGap = 1 << 3,
    GridTooFar = 1 << 4,
    ModelDivergence = 1 << 5,
    InvalidValue = 1 << 6,
    ParseFailed = 1 << 7,
    DirectionSemanticsUnknown = 1 << 8,
    ProviderUnavailable = 1 << 9
}
