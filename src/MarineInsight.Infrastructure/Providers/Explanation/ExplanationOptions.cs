namespace MarineInsight.Infrastructure.Providers.Explanation;

public sealed class ExplanationOptions
{
    public const string SectionName = "AI";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gpt-4o-mini";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(8);

    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromHours(24);

    public void Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("AI base URL must be an absolute HTTP or HTTPS URL.");
        }

        // Local models such as Ollama are reachable over HTTP without a credential;
        // cloud providers require a key. Fail fast rather than send an unauthenticated request.
        if (Enabled && string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("AI API key is required when the provider is enabled.");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("AI model name is required.");
        }

        if (Timeout < TimeSpan.FromSeconds(1) || Timeout > TimeSpan.FromSeconds(30))
        {
            throw new InvalidOperationException("AI timeout must be between 1 and 30 seconds.");
        }

        if (CacheLifetime < TimeSpan.FromHours(1) || CacheLifetime > TimeSpan.FromDays(2))
        {
            throw new InvalidOperationException("AI cache lifetime must be between 1 and 48 hours.");
        }
    }
}
