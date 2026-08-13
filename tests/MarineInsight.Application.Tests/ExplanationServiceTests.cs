using MarineInsight.Application.Analysis;
using MarineInsight.Application.Analysis.Ports;
using MarineInsight.Application.Errors;
using MarineInsight.Domain.Analysis;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarineInsight.Application.Tests;

public sealed class ExplanationServiceTests
{
    [Fact]
    public async Task DisabledProviderReturnsTemplateWithoutCallingIt()
    {
        var provider = new FakeExplanationProvider { IsEnabled = false };
        var service = CreateService(provider);
        var result = AnalysisTestFactory.CreateResult([ActivityType.Boat]);

        var explanation = await service.GenerateAsync(result, default);

        Assert.Equal(ExplanationSource.Template, explanation.Source);
        Assert.False(explanation.Degraded);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task ValidAiExplanationIsCachedAndReused()
    {
        var provider = new FakeExplanationProvider
        {
            Candidate = new ExplanationCandidate
            {
                Headline = "整体海况良好，适宜乘船活动。",
                Summary = "综合评分约 72 分，风浪较小。",
                ActivityNotes = [new ExplanationActivityNote { Activity = "boat", Text = "可以安排乘船活动。" }]
            }
        };
        var service = CreateService(provider);
        var result = AnalysisTestFactory.CreateResult([ActivityType.Boat]);

        var first = await service.GenerateAsync(result, default);
        var second = await service.GenerateAsync(result, default);

        Assert.Equal(ExplanationSource.Ai, first.Source);
        Assert.False(first.Degraded);
        Assert.Equal("fake-model", first.ModelVersion);
        Assert.Equal(ExplanationSource.Ai, second.Source);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task InvalidCandidateDegradesToTemplate()
    {
        var provider = new FakeExplanationProvider
        {
            Candidate = new ExplanationCandidate
            {
                Headline = "非常适宜，放心出海。",
                Summary = "海况理想。",
                ActivityNotes = []
            }
        };
        var service = CreateService(provider);
        var result = AnalysisTestFactory.CreateResult([ActivityType.Boat], riskLevel: RiskLevel.Caution);

        var explanation = await service.GenerateAsync(result, default);

        Assert.Equal(ExplanationSource.Template, explanation.Source);
        Assert.True(explanation.Degraded);
    }

    [Fact]
    public async Task ProviderFailureDegradesToTemplate()
    {
        var provider = new FakeExplanationProvider
        {
            Failure = new ProviderTimeoutException("fake-ai", "timed out")
        };
        var service = CreateService(provider);
        var result = AnalysisTestFactory.CreateResult([ActivityType.Boat]);

        var explanation = await service.GenerateAsync(result, default);

        Assert.Equal(ExplanationSource.Template, explanation.Source);
        Assert.True(explanation.Degraded);
    }

    private static ExplanationService CreateService(IExplanationProvider? provider) => new(
        provider is null ? null : [provider],
        new FakeExplanationCache(),
        ExplanationCachePolicy.Default,
        NullLogger<ExplanationService>.Instance);

    private sealed class FakeExplanationProvider : IExplanationProvider
    {
        public string ProviderCode => "fake-ai";

        public string ModelVersion => "fake-model";

        public bool IsEnabled { get; set; } = true;

        public int CallCount { get; private set; }

        public ExplanationCandidate? Candidate { get; set; }

        public ProviderException? Failure { get; set; }

        public Task<ExplanationCandidate> ExplainAsync(
            ExplanationFacts facts,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(Candidate ?? new ExplanationCandidate());
        }
    }

    private sealed class FakeExplanationCache : IExplanationCache
    {
        private readonly Dictionary<string, AnalysisExplanation> _entries = [];

        public Task<AnalysisExplanation?> GetAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _entries.TryGetValue(key, out var entry);
            return Task.FromResult(entry);
        }

        public Task SetAsync(
            string key,
            AnalysisExplanation explanation,
            ExplanationCachePolicy policy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _entries[key] = explanation;
            return Task.CompletedTask;
        }
    }
}
