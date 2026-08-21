using System.Text.Json.Serialization;

namespace MarineInsight.Infrastructure.Providers.WorldTides;

internal sealed record WorldTidesResponse
{
    [JsonPropertyName("status")]
    public int? Status { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("responseLat")]
    public double? ResponseLatitude { get; init; }

    [JsonPropertyName("responseLon")]
    public double? ResponseLongitude { get; init; }

    [JsonPropertyName("heights")]
    public WorldTidesHeight[]? Heights { get; init; }

    [JsonPropertyName("extremes")]
    public WorldTidesExtreme[]? Extremes { get; init; }

    [JsonPropertyName("credits")]
    public int? RemainingCredits { get; init; }

    [JsonPropertyName("callCount")]
    public int? CallCount { get; init; }
}

internal sealed record WorldTidesHeight
{
    [JsonPropertyName("dt")]
    public long Timestamp { get; init; }

    [JsonPropertyName("height")]
    public double HeightM { get; init; }
}

internal sealed record WorldTidesExtreme
{
    [JsonPropertyName("dt")]
    public long Timestamp { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }
}
