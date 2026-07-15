using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Persistence;

public sealed class MarineInsightDbContext(DbContextOptions<MarineInsightDbContext> options) : DbContext(options)
{
    public DbSet<LocationEntity> Locations => Set<LocationEntity>();

    public DbSet<ForecastBatchEntity> ForecastBatches => Set<ForecastBatchEntity>();

    public DbSet<ForecastPointEntity> ForecastPoints => Set<ForecastPointEntity>();

    public DbSet<ForecastPointSourceEntity> ForecastPointSources => Set<ForecastPointSourceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MarineInsightDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
