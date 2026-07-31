using MarineInsight.Application.Analysis;
using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Tests;

public sealed class MarineAnalysisCacheIdentityTests
{
    private static readonly ForecastRange Range = new(
        new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
        24);
    private static readonly GeoPoint Location = new(30.194, 122.687);

    [Fact]
    public void CreateNormalizesSourceAndActivityOrder()
    {
        var weather = CreateSource(
            "11111111-1111-1111-1111-111111111111",
            ForecastDataDomain.Weather,
            "open-meteo",
            "weather-v1");
        var marine = CreateSource(
            "22222222-2222-2222-2222-222222222222",
            ForecastDataDomain.Marine,
            "open-meteo",
            "marine-v1");

        var original = MarineAnalysisCacheIdentity.Create(
            [weather, marine],
            [ActivityType.Camping, ActivityType.Boat, ActivityType.Boat],
            "Marine-Score-1.0.0");
        var reordered = MarineAnalysisCacheIdentity.Create(
            [marine, weather],
            [ActivityType.Boat, ActivityType.Camping],
            "marine-score-1.0.0");

        Assert.Equal(original.Value, reordered.Value);
        Assert.Equal(original.ETag, reordered.ETag);
        Assert.Equal([ActivityType.Boat, ActivityType.Camping], original.Activities);
        Assert.Equal("marine-score-1.0.0", original.AlgorithmVersion);
    }

    [Fact]
    public void CreateSeparatesAlgorithmVersionAndActivitySet()
    {
        var sources = new[]
        {
            CreateSource(
                "11111111-1111-1111-1111-111111111111",
                ForecastDataDomain.Weather,
                "open-meteo",
                "weather-v1"),
            CreateSource(
                "22222222-2222-2222-2222-222222222222",
                ForecastDataDomain.Marine,
                "open-meteo",
                "marine-v1")
        };

        var original = MarineAnalysisCacheIdentity.Create(sources, [ActivityType.Boat], "marine-score-1.0.0");
        var otherAlgorithm = MarineAnalysisCacheIdentity.Create(sources, [ActivityType.Boat], "marine-score-2.0.0");
        var otherActivities = MarineAnalysisCacheIdentity.Create(sources, [ActivityType.Photography], "marine-score-1.0.0");

        Assert.NotEqual(original.Value, otherAlgorithm.Value);
        Assert.NotEqual(original.ETag, otherAlgorithm.ETag);
        Assert.NotEqual(original.Value, otherActivities.Value);
        Assert.NotEqual(original.ETag, otherActivities.ETag);
    }

    private static SourceBatchReference CreateSource(
        string batchId,
        ForecastDataDomain dataDomain,
        string providerCode,
        string model) =>
        new(
            Guid.Parse(batchId),
            dataDomain,
            new ProviderIdentity(providerCode, model),
            Location,
            null,
            Range.StartUtc.AddHours(-1),
            Range.StartUtc.AddHours(-1),
            Range,
            DataQuality.Valid());
}
