namespace MarineInsight.Infrastructure.Providers.Tianditu;

public sealed class TiandituOptions
{
    public const string SectionName = "Map:Tianditu";

    public string BaseUrl { get; set; } = "https://api.tianditu.gov.cn";

    public string? Key { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public void Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Tianditu base URL must be an absolute HTTPS URL.");
        }

        if (Timeout < TimeSpan.FromSeconds(1) || Timeout > TimeSpan.FromSeconds(30))
        {
            throw new InvalidOperationException("Tianditu timeout must be between 1 and 30 seconds.");
        }
    }
}
