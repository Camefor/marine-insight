using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarineInsight.Application.Analysis;
using MarineInsight.Application.Analysis.Ports;
using MarineInsight.Application.Errors;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Providers.Explanation;

/// <summary>
/// OpenAI-compatible Chat Completions adapter. A single implementation covers
/// OpenAI, DeepSeek, Qwen, Moonshot and Ollama by switching <c>AI:BaseUrl</c> and
/// <c>AI:Model</c>. The model is asked for JSON via <c>response_format: json_object</c>
/// and its output is deserialized into <see cref="ExplanationCandidate"/> for strict
/// local validation; no vendor-specific structured-output API is assumed.
/// </summary>
public sealed class OpenAiCompatibleExplanationProvider(
    HttpClient httpClient,
    IOptions<ExplanationOptions> options) : IExplanationProvider
{
    private const string Code = "openai-compatible";
    private const double Temperature = 0.2;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public string ProviderCode => Code;

    public string ModelVersion => options.Value.Model;

    public bool IsEnabled => options.Value.Enabled;

    public async Task<ExplanationCandidate> ExplainAsync(
        ExplanationFacts facts,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            throw new ProviderException(Code, ProviderFailureKind.Unavailable, "AI explanation provider is disabled.", false);
        }

        var request = BuildRequest(facts, settings);
        var uri = new Uri(settings.BaseUrl.TrimEnd('/') + "/chat/completions", UriKind.Absolute);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.Timeout);
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, uri);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            message.Content = JsonContent.Create(request, options: JsonOptions);

            using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new ProviderAuthenticationException(Code, "AI provider rejected the configured credential.");
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new ProviderRateLimitedException(Code, "AI provider rate-limited the request.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new ProviderException(Code, ProviderFailureKind.Unavailable, $"AI provider returned HTTP {(int)response.StatusCode}.", (int)response.StatusCode >= 500);
            }

            var payload = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(JsonOptions, timeout.Token)
                ?? throw new ProviderContractException(Code, "AI provider returned an empty response.");

            var content = payload.Choices is { Count: > 0 } ? payload.Choices[0].Message?.Content : null;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ProviderContractException(Code, "AI provider returned no message content.");
            }

            return DeserializeCandidate(content);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderTimeoutException(Code, "AI provider request timed out.", exception);
        }
        catch (JsonException exception)
        {
            throw new ProviderContractException(Code, "AI provider response JSON is invalid.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderException(Code, ProviderFailureKind.Unavailable, "AI provider could not be reached.", true, innerException: exception);
        }
    }

    private static OpenAiChatRequest BuildRequest(ExplanationFacts facts, ExplanationOptions settings)
    {
        var factsJson = JsonSerializer.Serialize(facts, JsonOptions);
        var messages = new[]
        {
            new OpenAiChatMessage("system", SystemPrompt),
            new OpenAiChatMessage("user", factsJson)
        };
        return new OpenAiChatRequest(
            settings.Model,
            messages,
            Temperature,
            new OpenAiResponseFormat("json_object"));
    }

    private static ExplanationCandidate DeserializeCandidate(string content)
    {
        var candidate = JsonSerializer.Deserialize<ExplanationCandidate>(content, JsonOptions);
        if (candidate is null)
        {
            throw new ProviderContractException(Code, "AI provider returned an empty explanation.");
        }

        return candidate;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private const string SystemPrompt = """
        你是海况分析结果的解释器，不是预报模型，也不承担任何安全责任。你只能引用用户消息 JSON 中的事实，不得推断、补充或猜测缺失值。

        硬性规则：
        1. 不得提高评分、降低风险、删除或弱化任何硬性风险，不得改变推荐窗口或返航时间。
        2. 数据置信度低或存在缺失指标时，优先说明不确定性。
        3. 只描述用户请求的活动（supportedActivities），不得自行引入其他活动。
        4. 文本中出现的数值、时间、地点、风险必须能追溯到输入事实，禁止编造。

        表达要求：使用简洁中文，先说结论，再给出最重要的 2-3 个原因。全文不使用 Markdown 标题、列表或加粗符号。

        输出必须严格符合以下 JSON Schema（只输出 JSON 对象本身）：
        {
          "headline": "一句话结论，不超过 40 个汉字",
          "summary": "2-3 句解释，说明主要原因",
          "activityNotes": [
            { "activity": "shoreFishing|boat|landing|camping|photography", "text": "针对该活动的简短建议" }
          ],
          "riskWindowText": "风险时间窗与返航建议，无则用 null",
          "uncertaintyText": "数据不确定性说明，无则用 null",
          "disclaimer": "固定免责声明文本"
        }

        activity 字段只能取值为：shoreFishing、boat、landing、camping、photography。
        disclaimer 字段必须原样返回："结果仅供辅助决策，请以官方预警和现场管理为准。"
        """;
}
