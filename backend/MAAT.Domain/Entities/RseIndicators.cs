namespace MAAT.Domain.Entities;

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

    // Social
    public double? EmployeeCountFte { get; private set; }
    public double? TurnoverRatePct { get; private set; }
    public double? TrainingHoursPerEmployee { get; private set; }
    public double? WorkAccidentRate { get; private set; }
    public double? GenderEqualityIndex { get; private set; }
    public double? PermanentContractPct { get; private set; }

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

    public void Update(
        double? co2EmissionsTons,
        double? energyConsumptionKwh,
        double? renewableEnergyPct,
        double? waterConsumptionM3,
        double? wasteTons,
        double? recyclingRatePct,
        double? employeeCountFte,
        double? turnoverRatePct,
        double? trainingHoursPerEmployee,
        double? workAccidentRate,
        double? genderEqualityIndex,
        double? permanentContractPct,
        double? localSuppliersPct,
        double? rseAssessedSuppliersPct,
        int? activeSuppliersCount,
        double? revenueEur,
        double? rseInvestmentEur,
        double? exportRevenuePct)
    {
        Co2EmissionsTons = co2EmissionsTons;
        EnergyConsumptionKwh = energyConsumptionKwh;
        RenewableEnergyPct = renewableEnergyPct;
        WaterConsumptionM3 = waterConsumptionM3;
        WasteTons = wasteTons;
        RecyclingRatePct = recyclingRatePct;
        EmployeeCountFte = employeeCountFte;
        TurnoverRatePct = turnoverRatePct;
        TrainingHoursPerEmployee = trainingHoursPerEmployee;
        WorkAccidentRate = workAccidentRate;
        GenderEqualityIndex = genderEqualityIndex;
        PermanentContractPct = permanentContractPct;
        LocalSuppliersPct = localSuppliersPct;
        RseAssessedSuppliersPct = rseAssessedSuppliersPct;
        ActiveSuppliersCount = activeSuppliersCount;
        RevenueEur = revenueEur;
        RseInvestmentEur = rseInvestmentEur;
        ExportRevenuePct = exportRevenuePct;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
