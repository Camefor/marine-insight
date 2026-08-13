using System.Diagnostics;
using MarineInsight.Application.Analysis.Ports;
using MarineInsight.Application.Errors;
using Microsoft.Extensions.Logging;

namespace MarineInsight.Application.Analysis;

/// <summary>
/// Orchestrates explanation generation: always builds the deterministic template
/// baseline, then attempts the AI provider when enabled, validating its output and
/// caching successful results. Any AI failure degrades to the template without
/// blocking the analysis.
/// </summary>
public sealed partial class ExplanationService
{
    private readonly IExplanationProvider? _provider;
    private readonly IExplanationCache _cache;
    private readonly ExplanationCachePolicy _cachePolicy;
    private readonly ILogger<ExplanationService> _logger;

    public ExplanationService(
        IEnumerable<IExplanationProvider>? providers,
        IExplanationCache cache,
        ExplanationCachePolicy cachePolicy,
        ILogger<ExplanationService> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);

        _provider = providers?.SingleOrDefault();
        _cache = cache;
        _cachePolicy = cachePolicy;
        _logger = logger;
    }

    public async Task<AnalysisExplanation> GenerateAsync(
        MarineAnalysisQueryResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        var facts = ExplanationFactsBuilder.Build(result);
        var provider = _provider;
        if (provider is null || !provider.IsEnabled)
        {
            return RuleTemplateExplanationGenerator.Generate(facts);
        }

        var cacheKey = ExplanationCacheIdentity.Create(
            result.CacheIdentity.Value,
            ExplanationDefaults.PromptVersion,
            provider.ModelVersion,
            ExplanationDefaults.Locale).Value;

        var cached = await _cache.GetAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var template = RuleTemplateExplanationGenerator.Generate(facts);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var candidate = await provider.ExplainAsync(facts, cancellationToken);
            var explanation = ExplanationValidator.TryValidate(candidate, facts, provider.ModelVersion);
            if (explanation is not null)
            {
                await _cache.SetAsync(cacheKey, explanation, _cachePolicy, cancellationToken);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    var elapsedMs = ElapsedMs(started);
                    LogAiGenerated(_logger, result.CacheIdentity.Value, elapsedMs);
                }

                return explanation;
            }

            return Degrade(template, result.CacheIdentity.Value, "validation_failed", started);
        }
        catch (ProviderException exception)
        {
            return Degrade(template, result.CacheIdentity.Value, $"{exception.FailureKind}: {exception.Message}", started);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Degrade(template, result.CacheIdentity.Value, "timeout", started);
        }
    }

    private AnalysisExplanation Degrade(
        AnalysisExplanation template,
        string analysisIdentity,
        string reason,
        long started)
    {
        LogAiDegraded(_logger, analysisIdentity, reason, ElapsedMs(started));
        return template with { Degraded = true };
    }

    private static long ElapsedMs(long started) =>
        (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "AI explanation generated for {AnalysisIdentity} in {ElapsedMs} ms.")]
    private static partial void LogAiGenerated(ILogger logger, string analysisIdentity, long elapsedMs);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "AI explanation for {AnalysisIdentity} degraded to the rule template ({Reason}, {ElapsedMs} ms).")]
    private static partial void LogAiDegraded(ILogger logger, string analysisIdentity, string reason, long elapsedMs);
}
