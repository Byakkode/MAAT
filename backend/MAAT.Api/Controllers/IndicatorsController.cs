using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Api.Controllers;

public sealed class RseIndicatorsDto
{
    public double? Co2EmissionsTons { get; init; }
    public double? EnergyConsumptionKwh { get; init; }
    public double? RenewableEnergyPct { get; init; }
    public double? WaterConsumptionM3 { get; init; }
    public double? WasteTons { get; init; }
    public double? RecyclingRatePct { get; init; }

    public double? EmployeeCountFte { get; init; }
    public double? TurnoverRatePct { get; init; }
    public double? TrainingHoursPerEmployee { get; init; }
    public double? WorkAccidentRate { get; init; }
    public double? GenderEqualityIndex { get; init; }
    public double? PermanentContractPct { get; init; }

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
public class IndicatorsController(MaatDbContext db) : ControllerBase
{
    [HttpGet("{year:int}")]
    public async Task<IActionResult> Get(int year, CancellationToken ct)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Unauthorized();

        var record = await db.RseIndicators
            .FirstOrDefaultAsync(r => r.CompanyId == companyId && r.Year == year, ct);

        if (record is null)
            return NoContent();

        return Ok(ToDto(record));
    }

    [HttpPut("{year:int}")]
    public async Task<IActionResult> Upsert(int year, [FromBody] RseIndicatorsDto dto, CancellationToken ct)
    {
        if (year < 2000 || year > 2100)
            return BadRequest(new { message = "Année invalide." });

        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Unauthorized();

        var record = await db.RseIndicators
            .FirstOrDefaultAsync(r => r.CompanyId == companyId && r.Year == year, ct);

        if (record is null)
        {
            record = new RseIndicators(companyId, year);
            db.RseIndicators.Add(record);
        }

        record.Update(
            dto.Co2EmissionsTons,
            dto.EnergyConsumptionKwh,
            dto.RenewableEnergyPct,
            dto.WaterConsumptionM3,
            dto.WasteTons,
            dto.RecyclingRatePct,
            dto.EmployeeCountFte,
            dto.TurnoverRatePct,
            dto.TrainingHoursPerEmployee,
            dto.WorkAccidentRate,
            dto.GenderEqualityIndex,
            dto.PermanentContractPct,
            dto.LocalSuppliersPct,
            dto.RseAssessedSuppliersPct,
            dto.ActiveSuppliersCount,
            dto.RevenueEur,
            dto.RseInvestmentEur,
            dto.ExportRevenuePct);

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(record));
    }

    [HttpGet("years")]
    public async Task<IActionResult> GetYears(CancellationToken ct)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Unauthorized();

        var years = await db.RseIndicators
            .Where(r => r.CompanyId == companyId)
            .Select(r => r.Year)
            .OrderByDescending(y => y)
            .ToListAsync(ct);

        return Ok(years);
    }

    private static RseIndicatorsDto ToDto(RseIndicators r) => new()
    {
        Co2EmissionsTons = r.Co2EmissionsTons,
        EnergyConsumptionKwh = r.EnergyConsumptionKwh,
        RenewableEnergyPct = r.RenewableEnergyPct,
        WaterConsumptionM3 = r.WaterConsumptionM3,
        WasteTons = r.WasteTons,
        RecyclingRatePct = r.RecyclingRatePct,
        EmployeeCountFte = r.EmployeeCountFte,
        TurnoverRatePct = r.TurnoverRatePct,
        TrainingHoursPerEmployee = r.TrainingHoursPerEmployee,
        WorkAccidentRate = r.WorkAccidentRate,
        GenderEqualityIndex = r.GenderEqualityIndex,
        PermanentContractPct = r.PermanentContractPct,
        LocalSuppliersPct = r.LocalSuppliersPct,
        RseAssessedSuppliersPct = r.RseAssessedSuppliersPct,
        ActiveSuppliersCount = r.ActiveSuppliersCount,
        RevenueEur = r.RevenueEur,
        RseInvestmentEur = r.RseInvestmentEur,
        ExportRevenuePct = r.ExportRevenuePct,
    };
}
