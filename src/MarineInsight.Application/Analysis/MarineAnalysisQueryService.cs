using MarineInsight.Application.Forecast;
using MarineInsight.Application.Forecast.Ports;
using MarineInsight.Domain.Forecast;

namespace MarineInsight.Application.Analysis;

/// <summary>
/// Fetches independent Weather and Marine batches, then creates the metrics-only snapshot.
/// </summary>
public sealed class MarineAnalysisQueryService
{
    private readonly IWeatherForecastProvider _weatherProvider;
    private readonly IMarineForecastProvider _marineProvider;
    private readonly IForecastCacheKeyFactory _cacheKeyFactory;
    private readonly ForecastBatchCacheCoordinator _cacheCoordinator;
    private readonly ForecastSnapshotAssembler _snapshotAssembler;

    public MarineAnalysisQueryService(
        IWeatherForecastProvider weatherProvider,
        IMarineForecastProvider marineProvider,
        IForecastCacheKeyFactory cacheKeyFactory,
        ForecastBatchCacheCoordinator cacheCoordinator,
        ForecastSnapshotAssembler snapshotAssembler)
    {
        ArgumentNullException.ThrowIfNull(weatherProvider);
        ArgumentNullException.ThrowIfNull(marineProvider);
        ArgumentNullException.ThrowIfNull(cacheKeyFactory);
        ArgumentNullException.ThrowIfNull(cacheCoordinator);
        ArgumentNullException.ThrowIfNull(snapshotAssembler);

        _weatherProvider = weatherProvider;
        _marineProvider = marineProvider;
        _cacheKeyFactory = cacheKeyFactory;
        _cacheCoordinator = cacheCoordinator;
        _snapshotAssembler = snapshotAssembler;
    }

    public async Task<MarineAnalysisQueryResult> ExecuteAsync(
        MarineAnalysisQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Weather and Marine are independent upstream requests. Starting both before
        // awaiting either keeps the normal query latency close to the slower source.
        var weatherTask = LoadWeatherAsync(query, cancellationToken);
        var marineTask = LoadMarineAsync(query, cancellationToken);
        await Task.WhenAll(weatherTask, marineTask);

        var weather = await weatherTask;
        var marine = await marineTask;
        var snapshot = _snapshotAssembler.Assemble(
            [weather.Batch, marine.Batch],
            query.Range);

        return new MarineAnalysisQueryResult(snapshot, weather, marine);
    }

    private Task<ForecastCacheResult> LoadWeatherAsync(
        MarineAnalysisQuery query,
        CancellationToken cancellationToken)
    {
        var key = _cacheKeyFactory.Create(
            ForecastDataDomain.Weather,
            _weatherProvider.Identity,
            query.Location,
            query.Range);

        return _cacheCoordinator.GetOrCreateAsync(
            key,
            _cacheKeyFactory.Policy,
            async providerCancellation =>
            {
                var result = await _weatherProvider.GetWeatherAsync(
                    query.Location,
                    query.Range,
                    providerCancellation);
                return result.Batch;
            },
            cancellationToken);
    }

    private Task<ForecastCacheResult> LoadMarineAsync(
        MarineAnalysisQuery query,
        CancellationToken cancellationToken)
    {
        var key = _cacheKeyFactory.Create(
            ForecastDataDomain.Marine,
            _marineProvider.Identity,
            query.Location,
            query.Range);

        return _cacheCoordinator.GetOrCreateAsync(
            key,
            _cacheKeyFactory.Policy,
            async providerCancellation =>
            {
                var result = await _marineProvider.GetMarineAsync(
                    query.Location,
                    query.Range,
                    providerCancellation);
                return result.Batch;
            },
            cancellationToken);
    }
}
