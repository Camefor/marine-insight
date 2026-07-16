using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Persistence;
using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Tests;

public sealed class ForecastBatchRepositoryTests
{
    [Theory]
    [InlineData(24)]
    [InlineData(72)]
    [InlineData(168)]
    public async Task AppendAndGetByIdRoundTripsBatchPointsSourcesAndMissingMetrics(int hours)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var locationId = Guid.NewGuid();
        await SeedLocationAsync(dbContext, locationId);

        var batch = CreateBatch(hours);
        var repository = new ForecastBatchRepository(dbContext);

        await repository.AppendAsync(locationId, batch);
        var result = await repository.GetByIdAsync(batch.BatchId);

        Assert.NotNull(result);
        Assert.Equal(batch.BatchId, result.BatchId);
        Assert.Equal(batch.Range, result.Range);
        Assert.Equal(batch.Points.Count, result.Points.Count);
        Assert.Equal(batch.Quality.Status, result.Quality.Status);
        Assert.Equal(batch.Quality.MissingMetrics, result.Quality.MissingMetrics);
        Assert.Equal(batch.Points[0].Metrics.WindSpeedMs, result.Points[0].Metrics.WindSpeedMs);
        Assert.Null(result.Points[0].Metrics.TemperatureC);
        Assert.Equal(batch.Points[0].Quality.MissingMetrics, result.Points[0].Quality.MissingMetrics);
        Assert.Equal(batch.Points[0].MetricSources[0].Metric, result.Points[0].MetricSources[0].Metric);
        Assert.Equal(batch.Points[0].MetricSources[0].Provider, result.Points[0].MetricSources[0].Provider);
        Assert.Equal(batch.Points[0].MetricSources[0].BatchId, result.Points[0].MetricSources[0].BatchId);
    }

    [Fact]
    public async Task FindFiltersByLocationProviderDomainAndCoveringRange()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var locationId = Guid.NewGuid();
        var otherLocationId = Guid.NewGuid();
        await SeedLocationAsync(dbContext, locationId);
        await SeedLocationAsync(dbContext, otherLocationId);

        var provider = new ProviderIdentity("test-provider", "test-model");
        var coveringBatch = CreateBatch(168, provider, ForecastDataDomain.Weather);
        var otherProviderBatch = CreateBatch(
            24,
            new ProviderIdentity("other-provider", "other-model"),
            ForecastDataDomain.Weather);
        var otherDomainBatch = CreateBatch(24, provider, ForecastDataDomain.Marine);
        var otherLocationBatch = CreateBatch(24, provider, ForecastDataDomain.Weather);
        var repository = new ForecastBatchRepository(dbContext);

        await repository.AppendAsync(locationId, coveringBatch);
        await repository.AppendAsync(locationId, otherProviderBatch);
        await repository.AppendAsync(locationId, otherDomainBatch);
        await repository.AppendAsync(otherLocationId, otherLocationBatch);

        var results = await repository.FindAsync(
            locationId,
            provider,
            ForecastDataDomain.Weather,
            new ForecastRange(coveringBatch.Range.StartUtc, 24));

        var result = Assert.Single(results);
        Assert.Equal(coveringBatch.BatchId, result.BatchId);
        Assert.Equal(169, result.Points.Count);
    }

    private static MarineInsightDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MarineInsightDbContext>()
            .UseSqlite(connection)
            .Options;
        return new MarineInsightDbContext(options);
    }

    private static async Task SeedLocationAsync(MarineInsightDbContext dbContext, Guid locationId)
    {
        dbContext.Locations.Add(new LocationEntity
        {
            Id = locationId,
            NormalizedName = $"location-{locationId:N}",
            DisplayName = "Test location",
            Latitude = 30.123456m,
            Longitude = 121.123456m,
            TimeZoneId = "Asia/Shanghai",
            LocationType = 0,
            IsPreset = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private static ForecastBatch CreateBatch(
        int hours,
        ProviderIdentity? provider = null,
        ForecastDataDomain dataDomain = ForecastDataDomain.Weather)
    {
        var batchId = Guid.NewGuid();
        var forecastProvider = provider ?? new ProviderIdentity("test-provider", "test-model");
        var range = new ForecastRange(new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), hours);
        var points = Enumerable.Range(0, hours + 1)
            .Select(offset =>
            {
                var forecastTime = range.StartUtc.AddHours(offset);
                var pointQuality = new DataQuality(
                    ForecastQualityStatus.Partial,
                    ForecastFreshness.Fresh,
                    0.5,
                    ForecastQualityMask.MissingMetric,
                    [ForecastMetricName.TemperatureC]);
                var source = new MetricSource(
                    ForecastMetricName.WindSpeedMs,
                    forecastProvider,
                    batchId,
                    forecastTime,
                    ForecastQualityStatus.Valid,
                    ForecastFreshness.Fresh);

                return new ForecastPoint(
                    forecastTime,
                    ForecastMetricSet.Create(windSpeedMs: 4.5 + offset),
                    pointQuality,
                    [source]);
            })
            .ToArray();

        return new ForecastBatch(
            batchId,
            dataDomain,
            forecastProvider,
            new GeoPoint(30.123456, 121.123456),
            new GeoPoint(30.1234, 121.1234),
            range.StartUtc.AddHours(-1),
            range.StartUtc.AddHours(-1),
            range,
            points,
            new DataQuality(
                ForecastQualityStatus.Partial,
                ForecastFreshness.Fresh,
                0.5,
                ForecastQualityMask.MissingMetric,
                [ForecastMetricName.TemperatureC]));
    }
}
