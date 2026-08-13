using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class AnalysisReportConfiguration : IEntityTypeConfiguration<AnalysisReportEntity>
{
    public void Configure(EntityTypeBuilder<AnalysisReportEntity> builder)
    {
        builder.ToTable("analysis_results");
        builder.HasKey(report => report.Id);
        builder.Property(report => report.Id)
            .HasColumnName("id");
        builder.Property(report => report.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        builder.Property(report => report.LocationId)
            .HasColumnName("location_id");
        builder.Property(report => report.RangeStartUtc)
            .HasColumnName("range_start")
            .IsRequired();
        builder.Property(report => report.RangeEndUtc)
            .HasColumnName("range_end")
            .IsRequired();
        builder.Property(report => report.Hours)
            .HasColumnName("hours")
            .IsRequired();
        builder.Property(report => report.AlgorithmVersion)
            .HasColumnName("algorithm_version")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(report => report.SourceSetHash)
            .HasColumnName("source_set_hash")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(report => report.ActivityType)
            .HasColumnName("activity_type");
        builder.Property(report => report.Score)
            .HasColumnName("score");
        builder.Property(report => report.RiskLevel)
            .HasColumnName("risk_level")
            .IsRequired();
        builder.Property(report => report.Confidence)
            .HasColumnName("confidence")
            .IsRequired();
        builder.Property(report => report.RecommendedStartUtc)
            .HasColumnName("recommended_start");
        builder.Property(report => report.RecommendedEndUtc)
            .HasColumnName("recommended_end");
        builder.Property(report => report.ReturnBeforeUtc)
            .HasColumnName("return_before");
        builder.Property(report => report.SummaryTemplateCode)
            .HasColumnName("summary_template_code")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(report => report.CreatedAtUtc)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne<MarineInsightUser>()
            .WithMany()
            .HasForeignKey(report => report.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(report => report.Risks)
            .WithOne(risk => risk.AnalysisResult)
            .HasForeignKey(risk => risk.AnalysisResultId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(report => report.SourceBatches)
            .WithOne(source => source.AnalysisResult)
            .HasForeignKey(source => source.AnalysisResultId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(report => new { report.UserId, report.CreatedAtUtc });
        builder.HasIndex(report => report.SourceSetHash);
    }
}
