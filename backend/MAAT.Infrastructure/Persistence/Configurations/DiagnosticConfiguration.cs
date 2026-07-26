using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class DiagnosticConfiguration : IEntityTypeConfiguration<Diagnostic>
{
    public void Configure(EntityTypeBuilder<Diagnostic> builder)
    {
        builder.ToTable("diagnostics");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(d => d.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(d => d.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.GlobalScore).HasColumnName("global_score").HasPrecision(5, 2);
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.CompletedAt).HasColumnName("completed_at");

        builder.HasMany<DomainScore>().WithOne().HasForeignKey(ds => ds.DiagnosticId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany<Response>().WithOne().HasForeignKey(r => r.DiagnosticId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany<DiagnosticRecommendation>().WithOne().HasForeignKey(dr => dr.DiagnosticId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany<Report>().WithOne().HasForeignKey(r => r.DiagnosticId).OnDelete(DeleteBehavior.Cascade);
    }
}
