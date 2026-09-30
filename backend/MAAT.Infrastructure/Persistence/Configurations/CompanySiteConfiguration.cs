using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class CompanySiteConfiguration : IEntityTypeConfiguration<CompanySite>
{
    public void Configure(EntityTypeBuilder<CompanySite> builder)
    {
        builder.ToTable("company_sites");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(CompanySite.NameMaxLength).IsRequired();
        builder.Property(s => s.Address).HasColumnName("address").HasMaxLength(CompanySite.AddressMaxLength).IsRequired();
        builder.Property(s => s.Tenure).HasColumnName("tenure").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Latitude).HasColumnName("latitude");
        builder.Property(s => s.Longitude).HasColumnName("longitude");
        builder.Property(s => s.GeocodedLabel).HasColumnName("geocoded_label").HasMaxLength(CompanySite.AddressMaxLength);
        builder.Property(s => s.InOrNearSensitiveArea).HasColumnName("in_or_near_sensitive_area");
        builder.Property(s => s.SensitiveAreaName).HasColumnName("sensitive_area_name").HasMaxLength(CompanySite.SensitiveAreaNameMaxLength);
        builder.Property(s => s.SensitiveAreasCheckedAt).HasColumnName("sensitive_areas_checked_at");
        builder.Property(s => s.DetectedSensitiveAreas).HasColumnName("detected_sensitive_areas").HasMaxLength(CompanySite.DetectedSensitiveAreasMaxLength);
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Supprimé avec l'entreprise (droit à l'effacement, auth-securite-rgpd.md section 6).
        builder.HasIndex(s => s.CompanyId);
        builder.HasOne<Company>().WithMany().HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}
