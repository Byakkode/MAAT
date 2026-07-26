using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class DiagnosticRecommendationConfiguration : IEntityTypeConfiguration<DiagnosticRecommendation>
{
    public void Configure(EntityTypeBuilder<DiagnosticRecommendation> builder)
    {
        builder.ToTable("diagnostic_recommendations");

        builder.HasKey(dr => new { dr.DiagnosticId, dr.RecommendationId });

        builder.Property(dr => dr.DiagnosticId).HasColumnName("diagnostic_id");
        builder.Property(dr => dr.RecommendationId).HasColumnName("recommendation_id");
        builder.Property(dr => dr.IsCompleted).HasColumnName("is_completed").IsRequired();
        builder.Property(dr => dr.CompletedAt).HasColumnName("completed_at");
        builder.Property(dr => dr.PriorityRank).HasColumnName("priority_rank").IsRequired();

        builder.HasOne<Recommendation>()
            .WithMany()
            .HasForeignKey(dr => dr.RecommendationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
