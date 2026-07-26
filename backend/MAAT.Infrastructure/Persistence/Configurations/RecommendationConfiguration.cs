using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.ToTable("recommendations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.Property(r => r.Domain).HasColumnName("domain").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.ActionText).HasColumnName("action_text").IsRequired();
        builder.Property(r => r.DetailText).HasColumnName("detail_text");
        builder.Property(r => r.ImpactPoints).HasColumnName("impact_points").HasPrecision(4, 2).IsRequired();
        builder.Property(r => r.EffortLevel).HasColumnName("effort_level").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.TriggerQuestionCode).HasColumnName("trigger_question_code").HasMaxLength(20).IsRequired();
        builder.Property(r => r.TriggerMaxValue).HasColumnName("trigger_max_value").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(r => r.Code).IsUnique();

        builder.HasOne<Question>()
            .WithMany()
            .HasForeignKey(r => r.TriggerQuestionCode)
            .HasPrincipalKey(q => q.Code)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
