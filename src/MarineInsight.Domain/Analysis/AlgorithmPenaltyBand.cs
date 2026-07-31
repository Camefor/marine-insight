namespace MarineInsight.Domain.Analysis;

public sealed record AlgorithmPenaltyBand
{
    public AlgorithmPenaltyBand(
        string metric,
        double? minInclusive,
        double? maxExclusive,
        double penalty)
    {
        Metric = metric;
        MinInclusive = minInclusive;
        MaxExclusive = maxExclusive;
        Penalty = penalty;
    }

    public string Metric { get; }

    public double? MinInclusive { get; }

    public double? MaxExclusive { get; }

    public double Penalty { get; }
}
