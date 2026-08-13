using MarineInsight.Domain.Analysis;
using MarineInsight.Domain.Forecast;
using MarineInsight.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Tests;

public sealed class AnalysisReportRepositoryTests
{
    [Fact]
    public async Task SaveAndGetByIdRoundTripsReportRisksAndSources()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var userId = Guid.NewGuid();
        await SeedUserAsync(dbContext, userId);

        var report = CreateReport(userId, RiskLevel.Caution);
        var repository = new AnalysisReportRepository(dbContext);

        await repository.SaveAsync(report);
        var result = await repository.GetByIdAsync(report.Id);

        Assert.NotNull(result);
        Assert.Equal(report.Id, result.Id);
        Assert.Equal(report.UserId, result.UserId);
        Assert.Null(result.LocationId);
        Assert.Equal(report.RangeStartUtc, result.RangeStartUtc);
        Assert.Equal(report.RangeEndUtc, result.RangeEndUtc);
        Assert.Equal(report.Hours, result.Hours);
        Assert.Equal(report.AlgorithmVersion, result.AlgorithmVersion);
        Assert.Equal(report.SourceSetHash, result.SourceSetHash);
        Assert.Equal(report.ActivityType, result.ActivityType);
        Assert.Equal(report.Score, result.Score);
        Assert.Equal(report.RiskLevel, result.RiskLevel);
        Assert.Equal(report.Confidence, result.Confidence);
        Assert.Equal(report.RecommendedStartUtc, result.RecommendedStartUtc);
        Assert.Equal(report.RecommendedEndUtc, result.RecommendedEndUtc);
        Assert.Equal(report.ReturnBeforeUtc, result.ReturnBeforeUtc);
        Assert.Equal(report.SummaryTemplateCode, result.SummaryTemplateCode);
        Assert.Equal(report.CreatedAtUtc, result.CreatedAtUtc);
        Assert.Equal(report.Risks.Count, result.Risks.Count);
        Assert.Equal(report.SourceBatches.Count, result.SourceBatches.Count);
    }

    [Fact]
    public async Task ListByUserIsNewestFirstAndIsolatedByOwner()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        await SeedUserAsync(dbContext, firstUser);
        await SeedUserAsync(dbContext, secondUser);

        var repository = new AnalysisReportRepository(dbContext);
        var older = CreateReport(firstUser, RiskLevel.Good, new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero));
        var newer = CreateReport(firstUser, RiskLevel.Moderate, new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero));
        var other = CreateReport(secondUser, RiskLevel.Avoid, new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero));

        await repository.SaveAsync(older);
        await repository.SaveAsync(newer);
        await repository.SaveAsync(other);

        var firstUserReports = await repository.ListByUserAsync(firstUser, 10);
        var secondUserReports = await repository.ListByUserAsync(secondUser, 10);

        Assert.Equal(2, firstUserReports.Count);
        Assert.Equal(newer.Id, firstUserReports[0].Id);
        Assert.Equal(older.Id, firstUserReports[1].Id);
        Assert.Single(secondUserReports);
        Assert.Equal(other.Id, secondUserReports[0].Id);
    }

    [Fact]
    public async Task NullableFieldsRoundTripAsNull()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var userId = Guid.NewGuid();
        await SeedUserAsync(dbContext, userId);

        var report = new AnalysisReport(
            Guid.NewGuid(),
            userId,
            null,
            new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero),
            24,
            "marine-score-1.0.0",
            "def456",
            null,
            null,
            RiskLevel.Unknown,
            0.3,
            null,
            null,
            null,
            "rule-template.v1",
            DateTimeOffset.UtcNow,
            [],
            []);
        var repository = new AnalysisReportRepository(dbContext);

        await repository.SaveAsync(report);
        var result = await repository.GetByIdAsync(report.Id);

        Assert.NotNull(result);
        Assert.Null(result.LocationId);
        Assert.Null(result.ActivityType);
        Assert.Null(result.Score);
        Assert.Equal(RiskLevel.Unknown, result.RiskLevel);
        Assert.Null(result.RecommendedStartUtc);
        Assert.Null(result.RecommendedEndUtc);
        Assert.Null(result.ReturnBeforeUtc);
        Assert.Empty(result.Risks);
        Assert.Empty(result.SourceBatches);
    }

    private static MarineInsightDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MarineInsightDbContext>()
            .UseSqlite(connection)
            .Options;
        return new MarineInsightDbContext(options);
    }

    private static async Task SeedUserAsync(MarineInsightDbContext dbContext, Guid userId)
    {
        dbContext.Users.Add(new MarineInsightUser
        {
            Id = userId,
            UserName = $"user-{userId:N}@example.com",
            NormalizedUserName = $"USER-{userId:N}@EXAMPLE.COM",
            Email = $"user-{userId:N}@example.com",
            NormalizedEmail = $"USER-{userId:N}@EXAMPLE.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await dbContext.SaveChangesAsync();
    }

    private static AnalysisReport CreateReport(
        Guid userId,
        RiskLevel riskLevel,
        DateTimeOffset? createdAtUtc = null)
    {
        var risks = new[]
        {
            new AnalysisRisk(
                new DateTimeOffset(2026, 7, 16, 2, 0, 0, TimeSpan.Zero),
                "swell-high",
                RiskSeverity.Warning,
                2.5,
                2.0,
                15,
                "长周期涌浪偏高")
        };
        var sources = new[]
        {
            new AnalysisSourceBatch(
                Guid.NewGuid(),
                ForecastDataDomain.Weather,
                "open-meteo",
                "weather-v1",
                AnalysisSourceRole.Primary,
                "forecast-snapshot-assembler.v1"),
            new AnalysisSourceBatch(
                Guid.NewGuid(),
                ForecastDataDomain.Tide,
                "world-tides",
                "tide-v1",
                AnalysisSourceRole.Enhancement,
                "forecast-snapshot-assembler.v1")
        };

        return new AnalysisReport(
            Guid.NewGuid(),
            userId,
            null,
            new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero),
            24,
            "marine-score-1.0.0",
            "abc123",
            ActivityType.Boat,
            72,
            riskLevel,
            0.8,
            new DateTimeOffset(2026, 7, 16, 3, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 16, 7, 0, 0, TimeSpan.Zero),
            "rule-template.v1",
            createdAtUtc ?? DateTimeOffset.UtcNow,
            risks,
            sources);
    }
}
