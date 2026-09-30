using MAAT.Application.Exceptions;
using MAAT.Application.UseCases;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAAT.Api.Controllers;

// docs/specs/norme-volontaire.md, section 4 : déclarations d'un exercice pour le module de base
// de la norme volontaire, et complétude B1 à B11. Les entités ne sortent pas d'ici : le
// contrôleur échange des DTO (CLAUDE.md, règles d'architecture).
[ApiController]
[Route("api/vsme")]
[Authorize]
public class VsmeController(VsmeService vsmeService) : ControllerBase
{
    [HttpGet("{year:int}")]
    public async Task<IActionResult> Get(int year, CancellationToken ct)
    {
        var statement = await vsmeService.GetStatementAsync(year, ct);
        return statement is null ? NoContent() : Ok(VsmeStatementDto.From(statement));
    }

    [HttpPut("{year:int}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Upsert(int year, [FromBody] VsmeStatementDto dto, CancellationToken ct)
    {
        try
        {
            var statement = await vsmeService.SaveStatementAsync(year, dto.ToValues(), ct);
            return Ok(VsmeStatementDto.From(statement));
        }
        catch (InvalidVsmeDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{year:int}/completeness")]
    public async Task<IActionResult> GetCompleteness(int year, CancellationToken ct)
    {
        var result = await vsmeService.GetCompletenessAsync(year, ct);
        return Ok(new VsmeCompletenessDto(
            result.IsCompliant,
            result.IsMicro,
            result.CompleteCount,
            [.. result.Disclosures.Select(d => new DisclosureCompletenessDto(d.Disclosure, d.State, d.Missing))]));
    }
}

public sealed record VsmeCompletenessDto(bool IsCompliant, bool IsMicro, int CompleteCount, IReadOnlyList<DisclosureCompletenessDto> Disclosures);

public sealed record DisclosureCompletenessDto(VsmeDisclosure Disclosure, DisclosureState State, IReadOnlyList<string> Missing);

public sealed class VsmeStatementDto
{
    public ReportingBasis? ReportingBasis { get; init; }
    public string? LegalForm { get; init; }
    public double? TotalAssetsEur { get; init; }
    public string? PrimaryCountry { get; init; }
    public EmployeeCountUnit? EmployeeCountUnit { get; init; }
    public List<VsmeDisclosure>? OmittedDisclosures { get; init; }
    public List<SubsidiaryDto>? Subsidiaries { get; init; }
    public List<CertificationDto>? Certifications { get; init; }

    public bool? HasPractices { get; init; }
    public bool? HasPolicies { get; init; }
    public bool? PoliciesPublic { get; init; }
    public bool? HasFutureInitiatives { get; init; }
    public bool? HasTargets { get; init; }
    public string? PracticesDescription { get; init; }
    public List<SustainabilityTopic>? CoveredTopics { get; init; }

    public bool? PollutionReportingApplicable { get; init; }
    public string? PollutionReportUrl { get; init; }
    public List<PollutantDto>? Pollutants { get; init; }

    public bool? CircularEconomyApplied { get; init; }
    public string? CircularEconomyDescription { get; init; }
    public string? MaterialFlowsDescription { get; init; }

    public List<CountryHeadcountDto>? EmployeesByCountry { get; init; }
    public bool? MinimumWageMet { get; init; }
    public int? CorruptionConvictions { get; init; }
    public double? CorruptionFinesEur { get; init; }

    // Un élément de liste sans nom (ligne laissée vide à l'écran) est ignoré plutôt que refusé.
    public VsmeStatementValues ToValues() => new()
    {
        ReportingBasis = ReportingBasis,
        LegalForm = LegalForm,
        TotalAssetsEur = TotalAssetsEur,
        PrimaryCountry = PrimaryCountry,
        EmployeeCountUnit = EmployeeCountUnit,
        OmittedDisclosures = OmittedDisclosures ?? [],
        Subsidiaries = [.. (Subsidiaries ?? []).Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .Select(s => new VsmeSubsidiary(s.Name!, s.RegisteredAddress ?? string.Empty))],
        Certifications = [.. (Certifications ?? []).Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .Select(c => new VsmeCertification(c.Name!, c.Issuer, c.ObtainedOn, c.Rating))],
        HasPractices = HasPractices,
        HasPolicies = HasPolicies,
        PoliciesPublic = PoliciesPublic,
        HasFutureInitiatives = HasFutureInitiatives,
        HasTargets = HasTargets,
        PracticesDescription = PracticesDescription,
        CoveredTopics = CoveredTopics ?? [],
        PollutionReportingApplicable = PollutionReportingApplicable,
        PollutionReportUrl = PollutionReportUrl,
        Pollutants = [.. (Pollutants ?? []).Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new VsmePollutant(p.Name!, p.Medium, p.Quantity, p.Unit ?? string.Empty))],
        CircularEconomyApplied = CircularEconomyApplied,
        CircularEconomyDescription = CircularEconomyDescription,
        MaterialFlowsDescription = MaterialFlowsDescription,
        EmployeesByCountry = [.. (EmployeesByCountry ?? []).Where(e => !string.IsNullOrWhiteSpace(e.Country))
            .Select(e => new VsmeCountryHeadcount(e.Country!, e.Employees))],
        MinimumWageMet = MinimumWageMet,
        CorruptionConvictions = CorruptionConvictions,
        CorruptionFinesEur = CorruptionFinesEur,
    };

    public static VsmeStatementDto From(VsmeStatement s) => new()
    {
        ReportingBasis = s.ReportingBasis,
        LegalForm = s.LegalForm,
        TotalAssetsEur = s.TotalAssetsEur,
        PrimaryCountry = s.PrimaryCountry,
        EmployeeCountUnit = s.EmployeeCountUnit,
        OmittedDisclosures = [.. s.OmittedDisclosures],
        Subsidiaries = [.. s.Subsidiaries.Select(x => new SubsidiaryDto { Name = x.Name, RegisteredAddress = x.RegisteredAddress })],
        Certifications = [.. s.Certifications.Select(x => new CertificationDto { Name = x.Name, Issuer = x.Issuer, ObtainedOn = x.ObtainedOn, Rating = x.Rating })],
        HasPractices = s.HasPractices,
        HasPolicies = s.HasPolicies,
        PoliciesPublic = s.PoliciesPublic,
        HasFutureInitiatives = s.HasFutureInitiatives,
        HasTargets = s.HasTargets,
        PracticesDescription = s.PracticesDescription,
        CoveredTopics = [.. s.CoveredTopics],
        PollutionReportingApplicable = s.PollutionReportingApplicable,
        PollutionReportUrl = s.PollutionReportUrl,
        Pollutants = [.. s.Pollutants.Select(x => new PollutantDto { Name = x.Name, Medium = x.Medium, Quantity = x.Quantity, Unit = x.Unit })],
        CircularEconomyApplied = s.CircularEconomyApplied,
        CircularEconomyDescription = s.CircularEconomyDescription,
        MaterialFlowsDescription = s.MaterialFlowsDescription,
        EmployeesByCountry = [.. s.EmployeesByCountry.Select(x => new CountryHeadcountDto { Country = x.Country, Employees = x.Employees })],
        MinimumWageMet = s.MinimumWageMet,
        CorruptionConvictions = s.CorruptionConvictions,
        CorruptionFinesEur = s.CorruptionFinesEur,
    };
}

public sealed class SubsidiaryDto
{
    public string? Name { get; init; }
    public string? RegisteredAddress { get; init; }
}

public sealed class CertificationDto
{
    public string? Name { get; init; }
    public string? Issuer { get; init; }
    public DateOnly? ObtainedOn { get; init; }
    public string? Rating { get; init; }
}

public sealed class PollutantDto
{
    public string? Name { get; init; }
    public PollutionMedium Medium { get; init; }
    public double Quantity { get; init; }
    public string? Unit { get; init; }
}

public sealed class CountryHeadcountDto
{
    public string? Country { get; init; }
    public double Employees { get; init; }
}
