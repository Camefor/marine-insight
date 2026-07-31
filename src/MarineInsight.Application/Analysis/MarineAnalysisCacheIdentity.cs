using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Analysis;

public sealed record MarineAnalysisCacheIdentity
{
    public const string DefaultSourceSelectionPolicy = "forecast-snapshot-assembler.v1";

    private const string Prefix = "mi:analysis:v1";

    private MarineAnalysisCacheIdentity(
        string value,
        string etag,
        string sourceBatchSetHash,
        string sourceSelectionPolicy,
        string algorithmVersion,
        IReadOnlyList<ActivityType> activities)
    {
        Value = value;
        ETag = etag;
        SourceBatchSetHash = sourceBatchSetHash;
        SourceSelectionPolicy = sourceSelectionPolicy;
        AlgorithmVersion = algorithmVersion;
        Activities = activities;
    }

    public string Value { get; }

    public string ETag { get; }

    public string SourceBatchSetHash { get; }

    public string SourceSelectionPolicy { get; }

    public string AlgorithmVersion { get; }

    public IReadOnlyList<ActivityType> Activities { get; }

    public static MarineAnalysisCacheIdentity Create(
        IEnumerable<SourceBatchReference> sourceBatches,
        IEnumerable<ActivityType>? activities,
        string algorithmVersion,
        string sourceSelectionPolicy = DefaultSourceSelectionPolicy)
    {
        ArgumentNullException.ThrowIfNull(sourceBatches);

        var sourceBatchArray = sourceBatches.ToArray();
        if (sourceBatchArray.Length == 0)
        {
            throw new ArgumentException("Analysis cache identity requires at least one source batch.", nameof(sourceBatches));
        }

        var normalizedPolicy = NormalizeSegment(sourceSelectionPolicy, nameof(sourceSelectionPolicy));
        var normalizedAlgorithmVersion = NormalizeSegment(algorithmVersion, nameof(algorithmVersion));
        var normalizedActivities = NormalizeActivities(activities);
        var sourceBatchSetHash = Hash(string.Join(
            "\n",
            sourceBatchArray
                .OrderBy(source => source.DataDomain)
                .ThenBy(source => source.BatchId)
                .Select(ToSourceBatchIdentity)));
        var activitySet = string.Join(",", normalizedActivities.Select(ToCacheName));

        // The key is semantic rather than transport-specific: if the source batches,
        // source selection policy, algorithm version or activities change, downstream
        // analysis caches and HTTP validators cannot accidentally reuse old output.
        var value = string.Join(
            ':',
            Prefix,
            Encode(normalizedPolicy),
            sourceBatchSetHash,
            Encode(normalizedAlgorithmVersion),
            string.IsNullOrEmpty(activitySet) ? "none" : Encode(activitySet));
        var etag = $"\"{Hash(value)[..32]}\"";

        return new MarineAnalysisCacheIdentity(
            value,
            etag,
            sourceBatchSetHash,
            normalizedPolicy,
            normalizedAlgorithmVersion,
            normalizedActivities);
    }

    private static ActivityType[] NormalizeActivities(IEnumerable<ActivityType>? activities) =>
        ActivityProfile.SelectDefaults(activities)
            .Select(profile => profile.ActivityType)
            .Distinct()
            .OrderBy(activity => activity)
            .ToArray();

    private static string ToSourceBatchIdentity(SourceBatchReference source)
    {
        var gridLocation = source.GridLocation is not { } gridPoint
            ? "none"
            : FormattableString.Invariant($"{gridPoint.Latitude:0.####},{gridPoint.Longitude:0.####}");

        return string.Join(
            '|',
            source.DataDomain,
            source.BatchId.ToString("D", CultureInfo.InvariantCulture),
            source.Provider.ProviderCode,
            source.Provider.SourceModel,
            FormattableString.Invariant($"{source.RequestedLocation.Latitude:0.####},{source.RequestedLocation.Longitude:0.####}"),
            gridLocation,
            source.IssuedAtUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            source.FetchedAtUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            source.Range.StartUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            source.Range.Hours.ToString(CultureInfo.InvariantCulture));
    }

    private static string NormalizeSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Analysis cache key segments cannot be empty.", parameterName);
        }

        return value.Trim().ToLowerInvariant();
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);

    private static string ToCacheName(ActivityType activity) =>
        activity.ToString().ToLowerInvariant();

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
