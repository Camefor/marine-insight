namespace MarineInsight.Infrastructure.Providers.WorldTides;

public sealed class WorldTidesOptions
{
    public const string SectionName = "TideProviders:WorldTides";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://www.worldtides.info/api/v3";

    public string? ApiKey { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromHours(12);

    public int CreditWarningThreshold { get; set; } = 100;

    public void Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("WorldTides base URL must be an absolute HTTPS URL.");
        }

        if (Enabled && string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("WorldTides API key is required when the provider is enabled.");
        }

        if (Timeout < TimeSpan.FromSeconds(1) || Timeout > TimeSpan.FromMinutes(2))
        {
            throw new InvalidOperationException("WorldTides timeout must be between 1 and 120 seconds.");
        }

        if (CacheLifetime < TimeSpan.FromHours(1) || CacheLifetime > TimeSpan.FromDays(2))
        {
            throw new InvalidOperationException("WorldTides cache lifetime must be between 1 and 48 hours.");
        }

        if (CreditWarningThreshold < 0)
        {
            throw new InvalidOperationException("WorldTides credit warning threshold cannot be negative.");
        }
    }
}
