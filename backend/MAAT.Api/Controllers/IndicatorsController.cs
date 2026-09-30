using MAAT.Application.Interfaces;
using MAAT.Application.UseCases;
using MAAT.Domain.Enums;
using MAAT.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAAT.Api.Controllers;

public sealed class RseIndicatorsDto
{
    public double? Co2EmissionsTons { get; init; }
    public double? EnergyConsumptionKwh { get; init; }
    public double? RenewableEnergyPct { get; init; }
    public double? WaterConsumptionM3 { get; init; }
    public double? WasteTons { get; init; }
    public double? RecyclingRatePct { get; init; }

    // docs/specs/norme-volontaire.md, section 2.
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

[ApiController]
[Route("api/indicators")]
[Authorize]
public class IndicatorsController(
    IRseIndicatorsRepository indicatorsRepository,
    CurrentPlanService currentPlan) : ControllerBase
{
    [HttpGet("{year:int}")]
    public async Task<IActionResult> Get(int year, CancellationToken ct)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Unauthorized();

        var record = await indicatorsRepository.GetByCompanyAndYearAsync(companyId, year, ct);

        if (record is null)
            return NoContent();

        return Ok(ToDto(record));
    }

    // Écriture réservée à Admin et User : un Viewer consulte, il ne déclare rien au nom de
    // l'entreprise (auth-securite-rgpd.md).
    [HttpPut("{year:int}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Upsert(int year, [FromBody] RseIndicatorsDto dto, CancellationToken ct)
    {
        if (year < 2000 || year > 2100)
            return BadRequest(new { message = "Année invalide." });

        // docs/specs/abonnement.md, section 8 : saisie ouverte dès Essential (le rapport selon
        // la norme volontaire en dépend, norme-volontaire.md), lecture ouverte à tous.
        await currentPlan.EnsureAsync(e => e.CanEditIndicators, SubscriptionPlan.Essential, ct);

        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Unauthorized();

        if (HasNegativeValue(dto))
            return BadRequest(new { message = "Un indicateur ne peut pas être négatif." });

        var record = await indicatorsRepository.GetByCompanyAndYearAsync(companyId, year, ct);

        if (record is null)
        {
            record = new RseIndicators(companyId, year);
            indicatorsRepository.Add(record);
        }

        record.Update(ToValues(dto));

        await indicatorsRepository.SaveAsync(ct);
        return Ok(ToDto(record));
    }

    [HttpGet("years")]
    public async Task<IActionResult> GetYears(CancellationToken ct)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Unauthorized();

        var years = await indicatorsRepository.GetYearsByCompanyAsync(companyId, ct);

        return Ok(years);
    }

    // Seuls les champs ajoutés pour la norme volontaire sont contrôlés : des quantités, des
    // effectifs et des taux, jamais négatifs. Les champs historiques gardent leur comportement.
    private static bool HasNegativeValue(RseIndicatorsDto d) =>
        new double?[]
        {
            d.ElectricityRenewableMwh, d.ElectricityNonRenewableMwh, d.FuelsRenewableMwh, d.FuelsNonRenewableMwh,
            d.Scope1Tco2e, d.Scope2LocationTco2e, d.WaterWithdrawalM3, d.WaterConsumptionStressM3,
            d.HazardousWasteTons, d.NonHazardousWasteTons, d.PermanentEmployees, d.TemporaryEmployees,
            d.FemaleEmployees, d.MaleEmployees, d.OtherGenderEmployees, d.RecordableAccidents, d.HoursWorked,
            d.WorkFatalities, d.CollectiveBargainingPct,
        }.Any(v => v < 0);

    private static RseIndicatorValues ToValues(RseIndicatorsDto d) => new()
    {
        Co2EmissionsTons = d.Co2EmissionsTons,
        EnergyConsumptionKwh = d.EnergyConsumptionKwh,
        RenewableEnergyPct = d.RenewableEnergyPct,
        WaterConsumptionM3 = d.WaterConsumptionM3,
        WasteTons = d.WasteTons,
        RecyclingRatePct = d.RecyclingRatePct,
        ElectricityRenewableMwh = d.ElectricityRenewableMwh,
        ElectricityNonRenewableMwh = d.ElectricityNonRenewableMwh,
        FuelsRenewableMwh = d.FuelsRenewableMwh,
        FuelsNonRenewableMwh = d.FuelsNonRenewableMwh,
        Scope1Tco2e = d.Scope1Tco2e,
        Scope2LocationTco2e = d.Scope2LocationTco2e,
        WaterWithdrawalM3 = d.WaterWithdrawalM3,
        WaterConsumptionStressM3 = d.WaterConsumptionStressM3,
        HazardousWasteTons = d.HazardousWasteTons,
        NonHazardousWasteTons = d.NonHazardousWasteTons,
        EmployeeCountFte = d.EmployeeCountFte,
        TurnoverRatePct = d.TurnoverRatePct,
        TrainingHoursPerEmployee = d.TrainingHoursPerEmployee,
        WorkAccidentRate = d.WorkAccidentRate,
        GenderEqualityIndex = d.GenderEqualityIndex,
        PermanentContractPct = d.PermanentContractPct,
        PermanentEmployees = d.PermanentEmployees,
        TemporaryEmployees = d.TemporaryEmployees,
        FemaleEmployees = d.FemaleEmployees,
        MaleEmployees = d.MaleEmployees,
        OtherGenderEmployees = d.OtherGenderEmployees,
        RecordableAccidents = d.RecordableAccidents,
        HoursWorked = d.HoursWorked,
        WorkFatalities = d.WorkFatalities,
        GenderPayGapPct = d.GenderPayGapPct,
        CollectiveBargainingPct = d.CollectiveBargainingPct,
        LocalSuppliersPct = d.LocalSuppliersPct,
        RseAssessedSuppliersPct = d.RseAssessedSuppliersPct,
        ActiveSuppliersCount = d.ActiveSuppliersCount,
        RevenueEur = d.RevenueEur,
        RseInvestmentEur = d.RseInvestmentEur,
        ExportRevenuePct = d.ExportRevenuePct,
    };

    private static RseIndicatorsDto ToDto(RseIndicators r) => new()
    {
        Co2EmissionsTons = r.Co2EmissionsTons,
        EnergyConsumptionKwh = r.EnergyConsumptionKwh,
        RenewableEnergyPct = r.RenewableEnergyPct,
        WaterConsumptionM3 = r.WaterConsumptionM3,
        WasteTons = r.WasteTons,
        RecyclingRatePct = r.RecyclingRatePct,
        ElectricityRenewableMwh = r.ElectricityRenewableMwh,
        ElectricityNonRenewableMwh = r.ElectricityNonRenewableMwh,
        FuelsRenewableMwh = r.FuelsRenewableMwh,
        FuelsNonRenewableMwh = r.FuelsNonRenewableMwh,
        Scope1Tco2e = r.Scope1Tco2e,
        Scope2LocationTco2e = r.Scope2LocationTco2e,
        WaterWithdrawalM3 = r.WaterWithdrawalM3,
        WaterConsumptionStressM3 = r.WaterConsumptionStressM3,
        HazardousWasteTons = r.HazardousWasteTons,
        NonHazardousWasteTons = r.NonHazardousWasteTons,
        EmployeeCountFte = r.EmployeeCountFte,
        TurnoverRatePct = r.TurnoverRatePct,
        TrainingHoursPerEmployee = r.TrainingHoursPerEmployee,
        WorkAccidentRate = r.WorkAccidentRate,
        GenderEqualityIndex = r.GenderEqualityIndex,
        PermanentContractPct = r.PermanentContractPct,
        PermanentEmployees = r.PermanentEmployees,
        TemporaryEmployees = r.TemporaryEmployees,
        FemaleEmployees = r.FemaleEmployees,
        MaleEmployees = r.MaleEmployees,
        OtherGenderEmployees = r.OtherGenderEmployees,
        RecordableAccidents = r.RecordableAccidents,
        HoursWorked = r.HoursWorked,
        WorkFatalities = r.WorkFatalities,
        GenderPayGapPct = r.GenderPayGapPct,
        CollectiveBargainingPct = r.CollectiveBargainingPct,
        LocalSuppliersPct = r.LocalSuppliersPct,
        RseAssessedSuppliersPct = r.RseAssessedSuppliersPct,
        ActiveSuppliersCount = r.ActiveSuppliersCount,
        RevenueEur = r.RevenueEur,
        RseInvestmentEur = r.RseInvestmentEur,
        ExportRevenuePct = r.ExportRevenuePct,
    };
}
