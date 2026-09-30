using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class VsmeStatementConfiguration : IEntityTypeConfiguration<VsmeStatement>
{
    public void Configure(EntityTypeBuilder<VsmeStatement> builder)
    {
        builder.ToTable("vsme_statements");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(s => s.Year).HasColumnName("year").IsRequired();

        builder.Property(s => s.ReportingBasis).HasColumnName("reporting_basis").HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.LegalForm).HasColumnName("legal_form").HasMaxLength(200);
        builder.Property(s => s.TotalAssetsEur).HasColumnName("total_assets_eur");
        builder.Property(s => s.PrimaryCountry).HasColumnName("primary_country").HasMaxLength(200);
        builder.Property(s => s.EmployeeCountUnit).HasColumnName("employee_count_unit").HasConversion<string>().HasMaxLength(20);

        // Listes d'énumérations : tableaux text[] de PostgreSQL, valeurs lisibles en base comme
        // les autres énumérations du schéma.
        builder.PrimitiveCollection(s => s.OmittedDisclosures).HasColumnName("omitted_disclosures")
            .ElementType(e => e.HasConversion<string>()).IsRequired();
        builder.PrimitiveCollection(s => s.CoveredTopics).HasColumnName("covered_topics")
            .ElementType(e => e.HasConversion<string>()).IsRequired();

        // Listes d'éléments : colonnes jsonb. Elles n'existent qu'à travers la déclaration de
        // l'exercice et ne sont jamais interrogées seules (modele-donnees.md).
        builder.OwnsMany(s => s.Subsidiaries, b => b.ToJson("subsidiaries"));
        builder.OwnsMany(s => s.Certifications, b => b.ToJson("certifications"));
        builder.OwnsMany(s => s.Pollutants, b =>
        {
            b.ToJson("pollutants");
            b.Property(p => p.Medium).HasConversion<string>();
        });
        builder.OwnsMany(s => s.EmployeesByCountry, b => b.ToJson("employees_by_country"));

        builder.Property(s => s.HasPractices).HasColumnName("has_practices");
        builder.Property(s => s.HasPolicies).HasColumnName("has_policies");
        builder.Property(s => s.PoliciesPublic).HasColumnName("policies_public");
        builder.Property(s => s.HasFutureInitiatives).HasColumnName("has_future_initiatives");
        builder.Property(s => s.HasTargets).HasColumnName("has_targets");
        builder.Property(s => s.PracticesDescription).HasColumnName("practices_description").HasMaxLength(2000);

        builder.Property(s => s.PollutionReportingApplicable).HasColumnName("pollution_reporting_applicable");
        builder.Property(s => s.PollutionReportUrl).HasColumnName("pollution_report_url").HasMaxLength(200);

        builder.Property(s => s.CircularEconomyApplied).HasColumnName("circular_economy_applied");
        builder.Property(s => s.CircularEconomyDescription).HasColumnName("circular_economy_description").HasMaxLength(2000);
        builder.Property(s => s.MaterialFlowsDescription).HasColumnName("material_flows_description").HasMaxLength(2000);

        builder.Property(s => s.MinimumWageMet).HasColumnName("minimum_wage_met");
        builder.Property(s => s.CorruptionConvictions).HasColumnName("corruption_convictions");
        builder.Property(s => s.CorruptionFinesEur).HasColumnName("corruption_fines_eur");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Une déclaration par entreprise et par exercice, supprimée avec l'entreprise (droit à
        // l'effacement, auth-securite-rgpd.md section 6).
        builder.HasIndex(s => new { s.CompanyId, s.Year }).IsUnique();
        builder.HasOne<Company>().WithMany().HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}
