using MarineInsight.Domain.Analysis;

namespace MarineInsight.Domain.Tests;

public sealed class AlgorithmVersionTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 7, 31, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] PassedGoldenSampleIds =
    [
        "GS-001",
        "GS-002",
        "GS-003",
        "GS-004",
        "GS-005",
        "GS-006",
        "GS-007",
        "GS-008",
        "GS-009",
        "GS-010"
    ];

    [Fact]
    public void CreateDefaultParametersPassPublicationValidation()
    {
        var parameters = MarineAlgorithmParameters.CreateDefault();

        var result = AlgorithmParameterValidator.ValidateForPublication(parameters, PassedGoldenSampleIds);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.StartsWith("sha256:", parameters.ConfigurationHash, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateForPublicationRejectsConfigurationHashMismatch()
    {
        var parameters = MarineAlgorithmParameters.CreateDefault()
            .WithConfigurationHash("sha256:tampered");

        var result = AlgorithmParameterValidator.ValidateForPublication(parameters, PassedGoldenSampleIds);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "CONFIGURATION_HASH_MISMATCH");
    }

    [Fact]
    public void ValidateForPublicationRequiresAllGoldenSamples()
    {
        var result = AlgorithmParameterValidator.ValidateForPublication(
            MarineAlgorithmParameters.CreateDefault(),
            PassedGoldenSampleIds.Where(id => id != "GS-010"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "GOLDEN_SAMPLE_REQUIRED" &&
            issue.Message.Contains("GS-010", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateForPublicationRejectsInvalidSafetyGateThreshold()
    {
        var source = MarineAlgorithmParameters.CreateDefault();
        var invalid = new MarineAlgorithmParameters(
            source.Version,
            source.SchemaVersion,
            source.SafetyGates with { WaveHeightM = -1 },
            source.PenaltyBands,
            source.CombinationRules,
            source.ActivityProfiles,
            source.Confidence,
            source.RecommendationWindow);

        var result = AlgorithmParameterValidator.ValidateForPublication(invalid, PassedGoldenSampleIds);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "SAFETY_GATE_WAVE_INVALID");
    }

    [Fact]
    public void ValidateForPublicationRejectsOverlappingPenaltyBands()
    {
        var source = MarineAlgorithmParameters.CreateDefault();
        var invalid = new MarineAlgorithmParameters(
            source.Version,
            source.SchemaVersion,
            source.SafetyGates,
            source.PenaltyBands.Append(new AlgorithmPenaltyBand("windSpeedMs", 4, 6, 8)),
            source.CombinationRules,
            source.ActivityProfiles,
            source.Confidence,
            source.RecommendationWindow);

        var result = AlgorithmParameterValidator.ValidateForPublication(invalid, PassedGoldenSampleIds);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "PENALTY_BAND_OVERLAP");
    }

    [Fact]
    public void ValidateForPublicationRequiresEveryActivityProfile()
    {
        var source = MarineAlgorithmParameters.CreateDefault();
        var invalid = new MarineAlgorithmParameters(
            source.Version,
            source.SchemaVersion,
            source.SafetyGates,
            source.PenaltyBands,
            source.CombinationRules,
            source.ActivityProfiles.Where(profile => profile.ActivityType != ActivityType.Photography),
            source.Confidence,
            source.RecommendationWindow);

        var result = AlgorithmParameterValidator.ValidateForPublication(invalid, PassedGoldenSampleIds);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "ACTIVITY_PROFILE_REQUIRED");
    }

    [Fact]
    public void AlgorithmVersionMovesFromDraftToValidatedToPublishedAndRetired()
    {
        var version = AlgorithmVersion.CreateDraft(
            Guid.NewGuid(),
            MarineAlgorithmParameters.CreateDefault(),
            "admin",
            CreatedAtUtc);

        var validation = version.ValidateForPublication(PassedGoldenSampleIds, "reviewer", CreatedAtUtc.AddHours(1));
        version.Publish(CreatedAtUtc.AddHours(2), "publisher", CreatedAtUtc.AddHours(2));
        version.Retire("operator", CreatedAtUtc.AddDays(1));

        Assert.True(validation.IsValid);
        Assert.Equal(AlgorithmVersionStatus.Retired, version.Status);
        Assert.Equal(CreatedAtUtc.AddHours(1), version.ValidatedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(2), version.PublishedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(2), version.EffectiveFromUtc);
        Assert.Equal(CreatedAtUtc.AddDays(1), version.RetiredAtUtc);
    }

    [Fact]
    public void AlgorithmVersionDoesNotValidateWhenPublicationChecksFail()
    {
        var version = AlgorithmVersion.CreateDraft(
            Guid.NewGuid(),
            MarineAlgorithmParameters.CreateDefault(),
            "admin",
            CreatedAtUtc);

        var validation = version.ValidateForPublication(["GS-001"], "reviewer", CreatedAtUtc.AddHours(1));

        Assert.False(validation.IsValid);
        Assert.Equal(AlgorithmVersionStatus.Draft, version.Status);
        Assert.Null(version.ValidatedAtUtc);
    }

    [Fact]
    public void AlgorithmVersionRequiresValidationBeforePublish()
    {
        var version = AlgorithmVersion.CreateDraft(
            Guid.NewGuid(),
            MarineAlgorithmParameters.CreateDefault(),
            "admin",
            CreatedAtUtc);

        Assert.Throws<InvalidOperationException>(() =>
            version.Publish(CreatedAtUtc.AddHours(1), "publisher", CreatedAtUtc.AddHours(1)));
    }

    [Fact]
    public void AlgorithmVersionCannotModifyPublishedParametersInPlace()
    {
        var version = AlgorithmVersion.CreateDraft(
            Guid.NewGuid(),
            MarineAlgorithmParameters.CreateDefault(),
            "admin",
            CreatedAtUtc);
        version.ValidateForPublication(PassedGoldenSampleIds, "reviewer", CreatedAtUtc.AddHours(1));
        version.Publish(CreatedAtUtc.AddHours(2), "publisher", CreatedAtUtc.AddHours(2));

        Assert.Throws<InvalidOperationException>(() =>
            version.ReplaceDraftParameters(MarineAlgorithmParameters.CreateDefault()));
        Assert.Throws<InvalidOperationException>(() =>
            version.ValidateForPublication(PassedGoldenSampleIds, "reviewer", CreatedAtUtc.AddHours(3)));
    }
}
