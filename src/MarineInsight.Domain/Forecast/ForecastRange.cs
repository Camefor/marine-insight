namespace MarineInsight.Domain.Forecast;

public readonly record struct ForecastRange
{
    public ForecastRange(DateTimeOffset start, int hours)
    {
        if (hours is not (24 or 72 or 168))
        {
            throw new ArgumentOutOfRangeException(nameof(hours), hours, "Forecast range must be 24, 72, or 168 hours.");
        }

        StartUtc = start.ToUniversalTime();
        Hours = hours;
    }

    public DateTimeOffset StartUtc { get; }

    public int Hours { get; }

    public DateTimeOffset EndUtc => StartUtc.AddHours(Hours);

    public bool Contains(DateTimeOffset forecastTime)
    {
        var utc = forecastTime.ToUniversalTime();
        return utc >= StartUtc && utc <= EndUtc;
    }
}
