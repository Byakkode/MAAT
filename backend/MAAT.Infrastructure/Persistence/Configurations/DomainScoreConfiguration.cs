using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class DomainScoreConfiguration : IEntityTypeConfiguration<DomainScore>
{
    public void Configure(EntityTypeBuilder<DomainScore> builder)
    {
        builder.ToTable("domain_scores");

        builder.HasKey(ds => new { ds.DiagnosticId, ds.Domain });

        builder.Property(ds => ds.DiagnosticId).HasColumnName("diagnostic_id");
        builder.Property(ds => ds.Domain).HasColumnName("domain").HasConversion<string>().HasMaxLength(20);
        builder.Property(ds => ds.Score).HasColumnName("score").HasPrecision(5, 2).IsRequired();
        builder.Property(ds => ds.SectorWeight).HasColumnName("sector_weight").HasPrecision(4, 3).IsRequired();
        builder.Property(ds => ds.Numerator).HasColumnName("numerator").HasPrecision(10, 2).IsRequired();
        builder.Property(ds => ds.Denominator).HasColumnName("denominator").HasPrecision(10, 2).IsRequired();
    }
}
