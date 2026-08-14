using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MarineInsight.Web.Authentication;

public sealed class CaptchaOptions
{
    public bool Enabled { get; set; } = true;

    public TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(3);
}

public sealed record CaptchaChallenge(string Id, string Svg, string Code);

/// <summary>
/// Server-side SVG captcha for account forms. The code is stored only as a SHA-256 hash in
/// process memory and is consumed exactly once, so a challenge cannot be replayed.
/// </summary>
public sealed class CaptchaService(
    IOptions<CaptchaOptions> options,
    IMemoryCache cache)
{
    // 去掉易混淆字符（0/O、1/I/L 及 2/Z、5/S、8/B、6/G、9/Q），仅保留大写字母与 3/4/7，
    // 避免用户把数字误认成字母或反之。
    private const string CharacterSet = "ACDEFHJKMNPRTUVWXY347";

    private static readonly string[] Colors =
    [
        "#173a35",
        "#1f6b5d",
        "#245e55",
        "#52736d",
        "#9a3329"
    ];

    public bool Enabled => options.Value.Enabled;

    public CaptchaChallenge Generate()
    {
        var code = GenerateCode();
        var id = Guid.NewGuid().ToString("N");
        cache.Set(
            CacheKey(id),
            Hash(code),
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = options.Value.Expiration
            });
        return new CaptchaChallenge(id, RenderSvg(code), code);
    }

    public bool Validate(string? id, string? code)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var key = CacheKey(id);
        if (!cache.TryGetValue(key, out string? storedHash) || storedHash is null)
        {
            return false;
        }

        // 一次性消费：无论成败都不再接受同一挑战。
        cache.Remove(key);

        var stored = Convert.FromHexString(storedHash);
        // 字符集仅含大写，输入统一去空格并转大写，避免大小写或输入法空格导致误判。
        var submitted = Convert.FromHexString(Hash(code.Trim().ToUpperInvariant()));
        return CryptographicOperations.FixedTimeEquals(stored, submitted);
    }

    private static string CacheKey(string id) => $"captcha:{id}";

    private static string GenerateCode()
    {
        Span<char> code = stackalloc char[4];
        for (var i = 0; i < code.Length; i++)
        {
            code[i] = CharacterSet[RandomNumberGenerator.GetInt32(CharacterSet.Length)];
        }

        return new string(code);
    }

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private static string RenderSvg(string code)
    {
        const int width = 120;
        const int height = 44;
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" role=\"img\" aria-label=\"验证码\">");
        builder.Append(CultureInfo.InvariantCulture, $"<rect width=\"{width}\" height=\"{height}\" fill=\"#eef3f2\"/>");
        for (var i = 0; i < 2; i++)
        {
            var x1 = Random.Shared.Next(0, width);
            var y1 = Random.Shared.Next(0, height);
            var x2 = Random.Shared.Next(0, width);
            var y2 = Random.Shared.Next(0, height);
            builder.Append(CultureInfo.InvariantCulture, $"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"#9db0ab\" stroke-width=\"1\"/>");
        }

        for (var i = 0; i < code.Length; i++)
        {
            var x = 16 + i * 26;
            var y = 30 + Random.Shared.Next(-6, 6);
            var rotate = Random.Shared.Next(-25, 25);
            var color = Colors[Random.Shared.Next(Colors.Length)];
            builder.Append(CultureInfo.InvariantCulture, $"<text x=\"{x}\" y=\"{y}\" transform=\"rotate({rotate} {x} {y})\" fill=\"{color}\" font-size=\"26\" font-family=\"Arial, sans-serif\" font-weight=\"700\">{code[i]}</text>");
        }

        builder.Append("</svg>");
        return builder.ToString();
    }
}
