namespace MAAT.Domain.Entities;

// Indicateurs chiffrés d'un exercice (écran Indicateurs). Une partie sert le module de base de
// la norme volontaire (docs/specs/norme-volontaire.md, section 2) ; le reste constitue les
// compléments propres à MAAT (achats responsables, économique…).
public class RseIndicators
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public int Year { get; private set; }

    // Environnement
    public double? Co2EmissionsTons { get; private set; }
    public double? EnergyConsumptionKwh { get; private set; }
    public double? RenewableEnergyPct { get; private set; }
    public double? WaterConsumptionM3 { get; private set; }
    public double? WasteTons { get; private set; }
    public double? RecyclingRatePct { get; private set; }

    // Norme volontaire, B3 §32 : ventilation de la consommation d'énergie, en MWh.
    public double? ElectricityRenewableMwh { get; private set; }
    public double? ElectricityNonRenewableMwh { get; private set; }
    public double? FuelsRenewableMwh { get; private set; }
    public double? FuelsNonRenewableMwh { get; private set; }

    // B3 §33 : Scope 2 selon la méthode fondée sur la localisation.
    public double? Scope1Tco2e { get; private set; }
    public double? Scope2LocationTco2e { get; private set; }

    // B6 §36 et §37 : le prélèvement est demandé à tous ; la consommation (WaterConsumptionM3)
    // et sa part en zone de stress hydrique seulement si les procédés consomment beaucoup d'eau.
    public double? WaterWithdrawalM3 { get; private set; }
    public double? WaterConsumptionStressM3 { get; private set; }

    // B7 §39 a.
    public double? HazardousWasteTons { get; private set; }
    public double? NonHazardousWasteTons { get; private set; }

    // Social
    public double? EmployeeCountFte { get; private set; }
    public double? TurnoverRatePct { get; private set; }
    public double? TrainingHoursPerEmployee { get; private set; }
    public double? WorkAccidentRate { get; private set; }
    public double? GenderEqualityIndex { get; private set; }
    public double? PermanentContractPct { get; private set; }

    // B8 §40 : en personnes ou en ETP selon VsmeStatement.EmployeeCountUnit, d'où des double.
    public double? PermanentEmployees { get; private set; }
    public double? TemporaryEmployees { get; private set; }
    public double? FemaleEmployees { get; private set; }
    public double? MaleEmployees { get; private set; }
    public double? OtherGenderEmployees { get; private set; }

    // B9 §41 : accidents entraînant un décès ou plus de trois jours d'absence (annexe A).
    public int? RecordableAccidents { get; private set; }
    public double? HoursWorked { get; private set; }
    public int? WorkFatalities { get; private set; }

    // B10 §42 b et c.
    public double? GenderPayGapPct { get; private set; }
    public double? CollectiveBargainingPct { get; private set; }

    // Achats responsables
    public double? LocalSuppliersPct { get; private set; }
    public double? RseAssessedSuppliersPct { get; private set; }
    public int? ActiveSuppliersCount { get; private set; }

    // Économique
    public double? RevenueEur { get; private set; }
    public double? RseInvestmentEur { get; private set; }
    public double? ExportRevenuePct { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private RseIndicators() { }

    public RseIndicators(Guid companyId, int year)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        Year = year;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(RseIndicatorValues values)
    {
        Co2EmissionsTons = values.Co2EmissionsTons;
        EnergyConsumptionKwh = values.EnergyConsumptionKwh;
        RenewableEnergyPct = values.RenewableEnergyPct;
        WaterConsumptionM3 = values.WaterConsumptionM3;
        WasteTons = values.WasteTons;
        RecyclingRatePct = values.RecyclingRatePct;

        ElectricityRenewableMwh = values.ElectricityRenewableMwh;
        ElectricityNonRenewableMwh = values.ElectricityNonRenewableMwh;
        FuelsRenewableMwh = values.FuelsRenewableMwh;
        FuelsNonRenewableMwh = values.FuelsNonRenewableMwh;
        Scope1Tco2e = values.Scope1Tco2e;
        Scope2LocationTco2e = values.Scope2LocationTco2e;
        WaterWithdrawalM3 = values.WaterWithdrawalM3;
        WaterConsumptionStressM3 = values.WaterConsumptionStressM3;
        HazardousWasteTons = values.HazardousWasteTons;
        NonHazardousWasteTons = values.NonHazardousWasteTons;

        EmployeeCountFte = values.EmployeeCountFte;
        TurnoverRatePct = values.TurnoverRatePct;
        TrainingHoursPerEmployee = values.TrainingHoursPerEmployee;
        WorkAccidentRate = values.WorkAccidentRate;
        GenderEqualityIndex = values.GenderEqualityIndex;
        PermanentContractPct = values.PermanentContractPct;

        PermanentEmployees = values.PermanentEmployees;
        TemporaryEmployees = values.TemporaryEmployees;
        FemaleEmployees = values.FemaleEmployees;
        MaleEmployees = values.MaleEmployees;
        OtherGenderEmployees = values.OtherGenderEmployees;
        RecordableAccidents = values.RecordableAccidents;
        HoursWorked = values.HoursWorked;
        WorkFatalities = values.WorkFatalities;
        GenderPayGapPct = values.GenderPayGapPct;
        CollectiveBargainingPct = values.CollectiveBargainingPct;

        LocalSuppliersPct = values.LocalSuppliersPct;
        RseAssessedSuppliersPct = values.RseAssessedSuppliersPct;
        ActiveSuppliersCount = values.ActiveSuppliersCount;
        RevenueEur = values.RevenueEur;
        RseInvestmentEur = values.RseInvestmentEur;
        ExportRevenuePct = values.ExportRevenuePct;

        ApplyTotals();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // norme-volontaire.md, section 2, règles de cohérence : un total dont toutes les parties
    // sont saisies en devient la somme. Sans cette règle, la ventilation et le total pourraient
    // se contredire dans le même rapport, et le tableau de bord (qui lit les totaux historiques)
    // afficherait un autre chiffre que la section 04.
    private void ApplyTotals()
    {
        if (ElectricityRenewableMwh is { } er && ElectricityNonRenewableMwh is { } en
            && FuelsRenewableMwh is { } fr && FuelsNonRenewableMwh is { } fn)
        {
            EnergyConsumptionKwh = (er + en + fr + fn) * 1000;
        }

        if (Scope1Tco2e is { } scope1 && Scope2LocationTco2e is { } scope2)
        {
            Co2EmissionsTons = scope1 + scope2;
        }

        if (HazardousWasteTons is { } hazardous && NonHazardousWasteTons is { } nonHazardous)
        {
            WasteTons = hazardous + nonHazardous;
        }
    }
}

// Valeurs saisies pour un exercice, toutes facultatives. Un record plutôt qu'une méthode à
// quarante paramètres positionnels : l'ordre des arguments ne peut plus inverser deux champs.
public sealed record RseIndicatorValues
{
    public double? Co2EmissionsTons { get; init; }
    public double? EnergyConsumptionKwh { get; init; }
    public double? RenewableEnergyPct { get; init; }
    public double? WaterConsumptionM3 { get; init; }
    public double? WasteTons { get; init; }
    public double? RecyclingRatePct { get; init; }

    public double? ElectricityRenewableMwh { get; init; }
    public double? ElectricityNonRenewableMwh { get; init; }
    public double? FuelsRenewableMwh { get; init; }
    public double? FuelsNonRenewableMwh { get; init; }
    public double? Scope1Tco2e { get; init; }
    public double? Scope2LocationTco2e { get; init; }
    public double? WaterWithdrawalM3 { get; init; }
    public double? WaterConsumptionStressM3 { get; init; }
    public double? HazardousWasteTons { get; init; }
    public double? NonHazardousWasteTons { get; init; }

    public double? EmployeeCountFte { get; init; }
    public double? TurnoverRatePct { get; init; }
    public double? TrainingHoursPerEmployee { get; init; }
    public double? WorkAccidentRate { get; init; }
    public double? GenderEqualityIndex { get; init; }
    public double? PermanentContractPct { get; init; }

    public double? PermanentEmployees { get; init; }
    public double? TemporaryEmployees { get; init; }
    public double? FemaleEmployees { get; init; }
    public double? MaleEmployees { get; init; }
    public double? OtherGenderEmployees { get; init; }
    public int? RecordableAccidents { get; init; }
    public double? HoursWorked { get; init; }
    public int? WorkFatalities { get; init; }
    public double? GenderPayGapPct { get; init; }
    public double? CollectiveBargainingPct { get; init; }

    public double? LocalSuppliersPct { get; init; }
    public double? RseAssessedSuppliersPct { get; init; }
    public int? ActiveSuppliersCount { get; init; }
    public double? RevenueEur { get; init; }
    public double? RseInvestmentEur { get; init; }
    public double? ExportRevenuePct { get; init; }
}
