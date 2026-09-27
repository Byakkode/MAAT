using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class CompanyLogoConfiguration : IEntityTypeConfiguration<CompanyLogo>
{
    public void Configure(EntityTypeBuilder<CompanyLogo> builder)
    {
        builder.ToTable("company_logos");

        // Un logo par entreprise (docs/specs/rapport-pdf.md, section 7) : la clé est
        // l'entreprise elle-même, supprimé avec elle (droit à l'effacement,
        // auth-securite-rgpd.md section 6).
        builder.HasKey(l => l.CompanyId);
        builder.Property(l => l.CompanyId).HasColumnName("company_id").ValueGeneratedNever();

        builder.Property(l => l.PngContent).HasColumnName("png_content").IsRequired();
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<Company>().WithOne().HasForeignKey<CompanyLogo>(l => l.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}
