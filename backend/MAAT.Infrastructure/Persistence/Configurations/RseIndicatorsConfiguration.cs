using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class RseIndicatorsConfiguration : IEntityTypeConfiguration<RseIndicators>
{
    public void Configure(EntityTypeBuilder<RseIndicators> builder)
    {
        builder.ToTable("rse_indicators");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(r => r.Year).HasColumnName("year").IsRequired();

        builder.Property(r => r.Co2EmissionsTons).HasColumnName("co2_emissions_tons");
        builder.Property(r => r.EnergyConsumptionKwh).HasColumnName("energy_consumption_kwh");
        builder.Property(r => r.RenewableEnergyPct).HasColumnName("renewable_energy_pct");
        builder.Property(r => r.WaterConsumptionM3).HasColumnName("water_consumption_m3");
        builder.Property(r => r.WasteTons).HasColumnName("waste_tons");
        builder.Property(r => r.RecyclingRatePct).HasColumnName("recycling_rate_pct");

        builder.Property(r => r.EmployeeCountFte).HasColumnName("employee_count_fte");
        builder.Property(r => r.TurnoverRatePct).HasColumnName("turnover_rate_pct");
        builder.Property(r => r.TrainingHoursPerEmployee).HasColumnName("training_hours_per_employee");
        builder.Property(r => r.WorkAccidentRate).HasColumnName("work_accident_rate");
        builder.Property(r => r.GenderEqualityIndex).HasColumnName("gender_equality_index");
        builder.Property(r => r.PermanentContractPct).HasColumnName("permanent_contract_pct");

        builder.Property(r => r.LocalSuppliersPct).HasColumnName("local_suppliers_pct");
        builder.Property(r => r.RseAssessedSuppliersPct).HasColumnName("rse_assessed_suppliers_pct");
        builder.Property(r => r.ActiveSuppliersCount).HasColumnName("active_suppliers_count");

        builder.Property(r => r.RevenueEur).HasColumnName("revenue_eur");
        builder.Property(r => r.RseInvestmentEur).HasColumnName("rse_investment_eur");
        builder.Property(r => r.ExportRevenuePct).HasColumnName("export_revenue_pct");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Une seule ligne par entreprise par année.
        builder.HasIndex(r => new { r.CompanyId, r.Year }).IsUnique();
    }
}
