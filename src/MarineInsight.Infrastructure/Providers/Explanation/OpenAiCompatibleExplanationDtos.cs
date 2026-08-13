using System.Text.Json.Serialization;

namespace MarineInsight.Infrastructure.Providers.Explanation;

internal sealed record OpenAiChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiChatMessage> Messages,
    [property: JsonPropertyName("temperature")] double Temperature,
    [property: JsonPropertyName("response_format")] OpenAiResponseFormat ResponseFormat);

internal sealed record OpenAiResponseFormat(
    [property: JsonPropertyName("type")] string Type);

internal sealed record OpenAiChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal sealed record OpenAiChatResponse(
    [property: JsonPropertyName("choices")] IReadOnlyList<OpenAiChatChoice>? Choices);

internal sealed record OpenAiChatChoice(
    [property: JsonPropertyName("message")] OpenAiChatMessage? Message);
