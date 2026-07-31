namespace MarineInsight.Domain.Analysis;

public sealed class AlgorithmVersion
{
    private AlgorithmVersion(
        Guid id,
        MarineAlgorithmParameters parameters,
        string createdBy,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Algorithm version id cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(parameters);
        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new ArgumentException("Creator is required.", nameof(createdBy));
        }

        Id = id;
        Parameters = parameters;
        CreatedBy = createdBy.Trim();
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        Status = AlgorithmVersionStatus.Draft;
    }

    public Guid Id { get; }

    public MarineAlgorithmParameters Parameters { get; private set; }

    public AlgorithmVersionStatus Status { get; private set; }

    public string CreatedBy { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public string? ValidatedBy { get; private set; }

    public DateTimeOffset? ValidatedAtUtc { get; private set; }

    public string? PublishedBy { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public DateTimeOffset? EffectiveFromUtc { get; private set; }

    public string? RetiredBy { get; private set; }

    public DateTimeOffset? RetiredAtUtc { get; private set; }

    public string Version => Parameters.Version;

    public string ConfigurationHash => Parameters.ConfigurationHash;

    public static AlgorithmVersion CreateDraft(
        Guid id,
        MarineAlgorithmParameters parameters,
        string createdBy,
        DateTimeOffset createdAtUtc) => new(
        id,
        parameters,
        createdBy,
        createdAtUtc);

    public void ReplaceDraftParameters(MarineAlgorithmParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        EnsureStatus(AlgorithmVersionStatus.Draft, "Only draft algorithm versions can be modified.");

        Parameters = parameters;
    }

    public AlgorithmParameterValidationResult ValidateForPublication(
        IEnumerable<string> passedGoldenSampleIds,
        string validatedBy,
        DateTimeOffset validatedAtUtc)
    {
        if (Status is AlgorithmVersionStatus.Published or AlgorithmVersionStatus.Retired)
        {
            throw new InvalidOperationException("Published or retired algorithm versions cannot be revalidated in place.");
        }

        if (string.IsNullOrWhiteSpace(validatedBy))
        {
            throw new ArgumentException("Validator is required.", nameof(validatedBy));
        }

        var result = AlgorithmParameterValidator.ValidateForPublication(Parameters, passedGoldenSampleIds);
        if (result.IsValid)
        {
            Status = AlgorithmVersionStatus.Validated;
            ValidatedBy = validatedBy.Trim();
            ValidatedAtUtc = validatedAtUtc.ToUniversalTime();
        }

        return result;
    }

    public void Publish(
        DateTimeOffset effectiveFromUtc,
        string publishedBy,
        DateTimeOffset publishedAtUtc)
    {
        EnsureStatus(AlgorithmVersionStatus.Validated, "Only validated algorithm versions can be published.");
        if (string.IsNullOrWhiteSpace(publishedBy))
        {
            throw new ArgumentException("Publisher is required.", nameof(publishedBy));
        }

        var publishedAt = publishedAtUtc.ToUniversalTime();
        var effectiveFrom = effectiveFromUtc.ToUniversalTime();
        if (effectiveFrom < publishedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectiveFromUtc),
                effectiveFromUtc,
                "Effective time cannot be earlier than publish time.");
        }

        Status = AlgorithmVersionStatus.Published;
        PublishedBy = publishedBy.Trim();
        PublishedAtUtc = publishedAt;
        EffectiveFromUtc = effectiveFrom;
    }

    public void Retire(
        string retiredBy,
        DateTimeOffset retiredAtUtc)
    {
        EnsureStatus(AlgorithmVersionStatus.Published, "Only published algorithm versions can be retired.");
        if (string.IsNullOrWhiteSpace(retiredBy))
        {
            throw new ArgumentException("Retirer is required.", nameof(retiredBy));
        }

        Status = AlgorithmVersionStatus.Retired;
        RetiredBy = retiredBy.Trim();
        RetiredAtUtc = retiredAtUtc.ToUniversalTime();
    }

    private void EnsureStatus(
        AlgorithmVersionStatus expected,
        string message)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(message);
        }
    }
}
