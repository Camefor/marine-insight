using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MarineInsight.Infrastructure.Persistence;

public sealed class MarineInsightDbContext(DbContextOptions<MarineInsightDbContext> options)
    : IdentityDbContext<MarineInsightUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<LocationEntity> Locations => Set<LocationEntity>();

    public DbSet<ForecastBatchEntity> ForecastBatches => Set<ForecastBatchEntity>();

    public DbSet<ForecastPointEntity> ForecastPoints => Set<ForecastPointEntity>();

    public DbSet<ForecastPointSourceEntity> ForecastPointSources => Set<ForecastPointSourceEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(MarineInsightDbContext).Assembly);

        builder.Entity<MarineInsightUser>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
    }
}
