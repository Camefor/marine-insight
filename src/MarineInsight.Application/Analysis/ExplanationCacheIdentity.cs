namespace MarineInsight.Application.Analysis;

public sealed record ExplanationCacheIdentity
{
    private const string Prefix = "mi:explanation:v1";

    private ExplanationCacheIdentity(string value) => Value = value;

    public string Value { get; }

    public static ExplanationCacheIdentity Create(
        string analysisIdentity,
        string promptVersion,
        string modelVersion,
        string locale,
        string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        // 解读文本包含当地时间，缓存键必须区分展示时区，避免不同时区共享同一段 AI 文本。
        var zoneToken = string.IsNullOrWhiteSpace(timeZoneId) ? "none" : timeZoneId;
        var value = string.Join(
            ':',
            Prefix,
            Encode(analysisIdentity),
            Encode(promptVersion),
            Encode(modelVersion),
            Encode(locale),
            Encode(zoneToken));
        return new ExplanationCacheIdentity(value);
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);
}
