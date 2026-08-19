namespace MarineInsight.Infrastructure.Providers.Tianditu;

public sealed class TiandituOptions
{
    public const string SectionName = "Map:Tianditu";

    public string BaseUrl { get; set; } = "https://api.tianditu.gov.cn";

    /// <summary>浏览器端 Key，仅用于前端 WMTS 瓦片请求。</summary>
    public string? Key { get; set; }

    /// <summary>服务端 Key，用于服务端调用天地图 /geocoder 逆地理编码。</summary>
    public string? ServerKey { get; set; }

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
