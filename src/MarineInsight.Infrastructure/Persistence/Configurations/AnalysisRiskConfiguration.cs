using MarineInsight.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarineInsight.Infrastructure.Persistence.Configurations;

public sealed class AnalysisRiskConfiguration : IEntityTypeConfiguration<AnalysisRiskEntity>
{
    public void Configure(EntityTypeBuilder<AnalysisRiskEntity> builder)
    {
        builder.ToTable("analysis_risks");
        builder.HasKey(risk => risk.Id);
        builder.Property(risk => risk.Id)
            .HasColumnName("id");
        builder.Property(risk => risk.AnalysisResultId)
            .HasColumnName("analysis_result_id")
            .IsRequired();
        builder.Property(risk => risk.ForecastTimeUtc)
            .HasColumnName("forecast_time")
            .IsRequired();
        builder.Property(risk => risk.RuleCode)
            .HasColumnName("rule_code")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(risk => risk.Severity)
            .HasColumnName("severity")
            .IsRequired();
        builder.Property(risk => risk.Actual)
            .HasColumnName("actual");
        builder.Property(risk => risk.Threshold)
            .HasColumnName("threshold");
        builder.Property(risk => risk.Penalty)
            .HasColumnName("penalty")
            .IsRequired();
        builder.Property(risk => risk.Message)
            .HasColumnName("message")
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(risk => new { risk.AnalysisResultId, risk.Severity });
    }
}
