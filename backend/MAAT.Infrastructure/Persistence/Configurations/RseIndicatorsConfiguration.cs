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

        // docs/specs/norme-volontaire.md, section 2 : données du module de base.
        builder.Property(r => r.ElectricityRenewableMwh).HasColumnName("electricity_renewable_mwh");
        builder.Property(r => r.ElectricityNonRenewableMwh).HasColumnName("electricity_non_renewable_mwh");
        builder.Property(r => r.FuelsRenewableMwh).HasColumnName("fuels_renewable_mwh");
        builder.Property(r => r.FuelsNonRenewableMwh).HasColumnName("fuels_non_renewable_mwh");
        builder.Property(r => r.Scope1Tco2e).HasColumnName("scope1_tco2e");
        builder.Property(r => r.Scope2LocationTco2e).HasColumnName("scope2_location_tco2e");
        builder.Property(r => r.WaterWithdrawalM3).HasColumnName("water_withdrawal_m3");
        builder.Property(r => r.WaterConsumptionStressM3).HasColumnName("water_consumption_stress_m3");
        builder.Property(r => r.HazardousWasteTons).HasColumnName("hazardous_waste_tons");
        builder.Property(r => r.NonHazardousWasteTons).HasColumnName("non_hazardous_waste_tons");
        builder.Property(r => r.PermanentEmployees).HasColumnName("permanent_employees");
        builder.Property(r => r.TemporaryEmployees).HasColumnName("temporary_employees");
        builder.Property(r => r.FemaleEmployees).HasColumnName("female_employees");
        builder.Property(r => r.MaleEmployees).HasColumnName("male_employees");
        builder.Property(r => r.OtherGenderEmployees).HasColumnName("other_gender_employees");
        builder.Property(r => r.RecordableAccidents).HasColumnName("recordable_accidents");
        builder.Property(r => r.HoursWorked).HasColumnName("hours_worked");
        builder.Property(r => r.WorkFatalities).HasColumnName("work_fatalities");
        builder.Property(r => r.GenderPayGapPct).HasColumnName("gender_pay_gap_pct");
        builder.Property(r => r.CollectiveBargainingPct).HasColumnName("collective_bargaining_pct");

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
