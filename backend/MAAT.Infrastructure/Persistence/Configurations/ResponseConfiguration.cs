using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class ResponseConfiguration : IEntityTypeConfiguration<Response>
{
    public void Configure(EntityTypeBuilder<Response> builder)
    {
        builder.ToTable("responses");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.DiagnosticId).HasColumnName("diagnostic_id").IsRequired();
        builder.Property(r => r.QuestionId).HasColumnName("question_id").IsRequired();
        builder.Property(r => r.Value).HasColumnName("value").IsRequired();
        builder.Property(r => r.AnsweredAt).HasColumnName("answered_at").IsRequired();

        builder.HasIndex(r => new { r.DiagnosticId, r.QuestionId }).IsUnique();

        builder.HasOne<Question>().WithMany().HasForeignKey(r => r.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}
