using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class AnalysisSourceBatchConfiguration : IEntityTypeConfiguration<AnalysisSourceBatchEntity>
{
    public void Configure(EntityTypeBuilder<AnalysisSourceBatchEntity> builder)
    {
        builder.ToTable("analysis_source_batches");
        builder.HasKey(source => new { source.AnalysisResultId, source.BatchId, source.SourceRole });
        builder.Property(source => source.AnalysisResultId)
            .HasColumnName("analysis_result_id")
            .IsRequired();
        builder.Property(source => source.BatchId)
            .HasColumnName("batch_id")
            .IsRequired();
        builder.Property(source => source.SourceRole)
            .HasColumnName("source_role")
            .IsRequired();
        builder.Property(source => source.DataDomain)
            .HasColumnName("data_domain")
            .IsRequired();
        builder.Property(source => source.ProviderCode)
            .HasColumnName("provider_code")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(source => source.SourceModel)
            .HasColumnName("source_model")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(source => source.SelectionPolicy)
            .HasColumnName("selection_policy")
            .HasMaxLength(120)
            .IsRequired();
    }
}
