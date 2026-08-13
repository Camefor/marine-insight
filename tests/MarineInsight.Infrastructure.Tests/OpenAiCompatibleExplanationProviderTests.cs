using System.Net;
using System.Text;
using System.Text.Json;
using MarineInsight.Application.Analysis;
using MarineInsight.Application.Errors;
using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Providers.Explanation;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Tests;

public sealed class OpenAiCompatibleExplanationProviderTests
{
    [Fact]
    public async Task ValidResponseDeserializesCandidate()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(ReadSample()));
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var candidate = await provider.ExplainAsync(CreateFacts(), default);

        Assert.Equal("整体海况良好，适宜乘船活动。", candidate.Headline);
        Assert.Equal("综合评分约 72 分，风浪较小。", candidate.Summary);
        var note = Assert.Single(candidate.ActivityNotes!);
        Assert.Equal("boat", note.Activity);
        Assert.Equal("可以安排乘船活动。", note.Text);
    }

    [Fact]
    public async Task AuthenticationRateLimitAndServerErrorsMapToProviderFailures()
    {
        using var unauthorizedClient = new HttpClient(new StubHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var unauthorized = CreateProvider(unauthorizedClient);
        await Assert.ThrowsAsync<ProviderAuthenticationException>(() =>
            unauthorized.ExplainAsync(CreateFacts(), default));

        using var limitedClient = new HttpClient(new StubHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        var limited = CreateProvider(limitedClient);
        await Assert.ThrowsAsync<ProviderRateLimitedException>(() =>
            limited.ExplainAsync(CreateFacts(), default));

        using var serverErrorClient = new HttpClient(new StubHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var serverError = CreateProvider(serverErrorClient);
        await Assert.ThrowsAsync<ProviderException>(() =>
            serverError.ExplainAsync(CreateFacts(), default));
    }

    [Fact]
    public async Task InvalidJsonMapsToContractFailure()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(
            _ => JsonResponse("{\"choices\":[{\"message\":{\"content\":\"not-json\"}}]}")));
        var provider = CreateProvider(client);

        await Assert.ThrowsAsync<ProviderContractException>(() =>
            provider.ExplainAsync(CreateFacts(), default));
    }

    [Fact]
    public async Task SlowResponseMapsToTimeoutFailure()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(
            _ => JsonResponse(ReadSample()),
            delay: TimeSpan.FromSeconds(1)));
        var provider = CreateProvider(client, timeout: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<ProviderTimeoutException>(() =>
            provider.ExplainAsync(CreateFacts(), default));
    }

    [Fact]
    public async Task FencedJsonResponseIsNormalized()
    {
        using var sample = JsonDocument.Parse(ReadSample());
        var content = sample.RootElement.GetProperty("choices")[0]
            .GetProperty("message").GetProperty("content").GetString()!;

        var fenced = $"```json\n{content}\n```";
        var responsePayload = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { role = "assistant", content = fenced } }
            }
        });

        using var client = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(responsePayload)));
        var provider = CreateProvider(client);

        var candidate = await provider.ExplainAsync(CreateFacts(), default);

        Assert.Equal("整体海况良好，适宜乘船活动。", candidate.Headline);
    }

    private static OpenAiCompatibleExplanationProvider CreateProvider(
        HttpClient client,
        TimeSpan? timeout = null) => new(
        client,
        Options.Create(new ExplanationOptions
        {
            Enabled = true,
            BaseUrl = "https://ai.test/v1",
            ApiKey = "test-key",
            Model = "gpt-4o-mini",
            Timeout = timeout ?? TimeSpan.FromSeconds(8)
        }));

    private static ExplanationFacts CreateFacts() => new(
        "东极岛",
        "Asia/Shanghai",
        new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero),
        24,
        ForecastQualityStatus.Valid,
        ForecastFreshness.Fresh,
        1.0,
        [],
        new ExplanationOverallFact(72, RiskLevel.Good, 0.85, "marine-score-1.0.0"),
        [new ExplanationActivityFact(ActivityType.Boat, 72, RiskLevel.Good)],
        [],
        [],
        ExplanationDefaults.Disclaimer,
        [ActivityType.Boat]);

    private static string ReadSample() => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "TestData", "Explanation", "openai-response.json"));

    private static HttpResponseMessage JsonResponse(string payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        TimeSpan? delay = null) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory = responseFactory;
        private readonly TimeSpan? _delay = delay;

        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (_delay is { } delay)
            {
                await Task.Delay(delay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return _responseFactory(request);
        }
    }
}
