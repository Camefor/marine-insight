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
        string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        var value = string.Join(
            ':',
            Prefix,
            Encode(analysisIdentity),
            Encode(promptVersion),
            Encode(modelVersion),
            Encode(locale));
        return new ExplanationCacheIdentity(value);
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);
}
