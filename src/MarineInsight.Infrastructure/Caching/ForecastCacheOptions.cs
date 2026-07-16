using MarineInsight.Application.Forecast;

namespace MarineInsight.Infrastructure.Caching;

public sealed class ForecastCacheOptions
{
    public const string SectionName = "Caching:Forecast";

    public string Environment { get; set; } = "development";

    public string NormalizerVersion { get; set; } = "v1";

    public int CoordinatePrecision { get; set; } = 4;

    public TimeSpan FreshLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan StaleIfErrorLifetime { get; set; } = TimeSpan.FromHours(2);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Environment))
        {
            throw new InvalidOperationException("Caching:Forecast:Environment is required.");
        }

        if (string.IsNullOrWhiteSpace(NormalizerVersion))
        {
            throw new InvalidOperationException("Caching:Forecast:NormalizerVersion is required.");
        }

        if (CoordinatePrecision is < 0 or > 6)
        {
            throw new InvalidOperationException(
                "Caching:Forecast:CoordinatePrecision must be between 0 and 6.");
        }

        if (FreshLifetime < TimeSpan.FromSeconds(1) || FreshLifetime > TimeSpan.FromHours(1))
        {
            throw new InvalidOperationException(
                "Caching:Forecast:FreshLifetime must be between 1 second and 1 hour.");
        }

        if (StaleIfErrorLifetime < TimeSpan.Zero || StaleIfErrorLifetime > TimeSpan.FromHours(24))
        {
            throw new InvalidOperationException(
                "Caching:Forecast:StaleIfErrorLifetime must be between 0 and 24 hours.");
        }
    }

    public ForecastCachePolicy ToPolicy()
    {
        Validate();
        return new ForecastCachePolicy(FreshLifetime, StaleIfErrorLifetime);
    }
}
