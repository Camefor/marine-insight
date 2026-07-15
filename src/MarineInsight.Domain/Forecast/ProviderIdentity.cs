namespace MarineInsight.Domain.Forecast;

public sealed record ProviderIdentity
{
    public ProviderIdentity(string providerCode, string sourceModel)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
        {
            throw new ArgumentException("Provider code is required.", nameof(providerCode));
        }

        if (string.IsNullOrWhiteSpace(sourceModel))
        {
            throw new ArgumentException("Source model is required.", nameof(sourceModel));
        }

        ProviderCode = providerCode.Trim().ToLowerInvariant();
        SourceModel = sourceModel.Trim();
    }

    public string ProviderCode { get; }

    public string SourceModel { get; }
}
