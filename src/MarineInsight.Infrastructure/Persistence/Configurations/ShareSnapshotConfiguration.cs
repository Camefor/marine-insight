using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class ShareSnapshotConfiguration : IEntityTypeConfiguration<ShareSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<ShareSnapshotEntity> builder)
    {
        builder.ToTable("share_snapshots");
        builder.HasKey(snapshot => snapshot.Token);
        builder.Property(snapshot => snapshot.Token).HasColumnName("token").HasMaxLength(64);
        builder.Property(snapshot => snapshot.Payload).HasColumnName("payload").IsRequired();
        builder.Property(snapshot => snapshot.CreatedAtUtc).HasColumnName("created_at").IsRequired();
        builder.Property(snapshot => snapshot.ExpiresAtUtc).HasColumnName("expires_at").IsRequired();
        builder.Property(snapshot => snapshot.RetainUntilUtc).HasColumnName("retain_until").IsRequired();
        builder.HasIndex(snapshot => snapshot.ExpiresAtUtc);
        builder.HasIndex(snapshot => snapshot.RetainUntilUtc);
    }
}
