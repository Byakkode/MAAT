using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.DiagnosticId).HasColumnName("diagnostic_id").IsRequired();
        builder.Property(r => r.Format).HasColumnName("format").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.GeneratedAt).HasColumnName("generated_at").IsRequired();
        builder.Property(r => r.GeneratedByUserId).HasColumnName("generated_by_user_id").IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.GeneratedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
