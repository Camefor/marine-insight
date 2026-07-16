using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace MarineInsight.Web.Observability;

public sealed class SensitiveDataEnricher : ILogEventEnricher
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly Regex BearerTokenPattern = new(
        "Bearer\\s+[^\\s,;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SensitiveTextPattern = new(
        "(?<name>api[-_]?key|access[-_]?token|refresh[-_]?token|token|authorization|password|secret|connection[-_]?string)\\s*[:=]\\s*(?<value>[^\\s,;]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        foreach (var property in logEvent.Properties.ToArray())
        {
            var sanitizedValue = IsSensitiveProperty(property.Key)
                ? new ScalarValue(RedactedValue)
                : SanitizeValue(property.Value, property.Key);

            logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, sanitizedValue));
        }
    }

    private static bool IsSensitiveProperty(string propertyName)
    {
        var normalizedName = propertyName.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalizedName.Contains("apikey", StringComparison.Ordinal)
            || normalizedName.Contains("password", StringComparison.Ordinal)
            || normalizedName.Contains("secret", StringComparison.Ordinal)
            || normalizedName.Contains("authorization", StringComparison.Ordinal)
            || normalizedName.Contains("cookie", StringComparison.Ordinal)
            || normalizedName.Contains("connectionstring", StringComparison.Ordinal)
            || normalizedName.Contains("accesstoken", StringComparison.Ordinal)
            || normalizedName.Contains("refreshtoken", StringComparison.Ordinal)
            || normalizedName is "token" or "userid" or "email"
            || normalizedName is "latitude" or "longitude" or "coordinate" or "coordinates";
    }

    private static LogEventPropertyValue SanitizeValue(LogEventPropertyValue value, string propertyName)
    {
        return value switch
        {
            ScalarValue scalar when scalar.Value is string text =>
                new ScalarValue(SanitizeText(text)),
            StructureValue structure => new StructureValue(
                structure.Properties.Select(property => new LogEventProperty(
                    property.Name,
                    IsSensitiveProperty(property.Name)
                        ? new ScalarValue(RedactedValue)
                        : SanitizeValue(property.Value, property.Name))),
                structure.TypeTag),
            SequenceValue sequence => new SequenceValue(
                sequence.Elements.Select(element => SanitizeValue(element, propertyName))),
            DictionaryValue dictionary => new DictionaryValue(
                dictionary.Elements.Select(element => new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                    element.Key,
                    SanitizeValue(element.Value, propertyName)))),
            _ => value
        };
    }

    private static string SanitizeText(string text)
    {
        var sanitized = BearerTokenPattern.Replace(text, $"Bearer {RedactedValue}");
        return SensitiveTextPattern.Replace(sanitized, "${name}=" + RedactedValue);
    }
}
