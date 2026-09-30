using MAAT.Application.Exceptions;
using MAAT.Application.UseCases;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAAT.Api.Controllers;

// docs/specs/norme-volontaire.md, section 4 : sites de l'entreprise (B1 géolocalisation, B5
// biodiversité). Le géocodage a lieu côté serveur à l'enregistrement (ADR 0013) ; la réponse
// dit s'il a abouti (geocoded), pour que l'écran propose de corriger l'adresse sinon.
[ApiController]
[Route("api/company/sites")]
[Authorize]
public class CompanySitesController(VsmeService vsmeService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok((await vsmeService.ListSitesAsync(ct)).Select(CompanySiteDto.From));

    [HttpPost]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Create([FromBody] CompanySiteInput input, CancellationToken ct)
    {
        try
        {
            var site = await vsmeService.CreateSiteAsync(input.ToDetails(), ct);
            return Ok(CompanySiteDto.From(site));
        }
        catch (InvalidVsmeDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (SiteLimitReachedException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CompanySiteInput input, CancellationToken ct)
    {
        try
        {
            var site = await vsmeService.UpdateSiteAsync(id, input.ToDetails(), ct);
            return site is null ? NotFound() : Ok(CompanySiteDto.From(site));
        }
        catch (InvalidVsmeDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await vsmeService.DeleteSiteAsync(id, ct);
        return NoContent();
    }
}

public sealed class CompanySiteInput
{
    public string? Name { get; init; }
    public string? Address { get; init; }
    public SiteTenure Tenure { get; init; }
    public bool? InOrNearSensitiveArea { get; init; }
    public string? SensitiveAreaName { get; init; }

    public CompanySiteDetails ToDetails() =>
        new(Name ?? string.Empty, Address ?? string.Empty, Tenure, InOrNearSensitiveArea, SensitiveAreaName);
}

public sealed record CompanySiteDto(
    Guid Id,
    string Name,
    string Address,
    SiteTenure Tenure,
    bool Geocoded,
    double? Latitude,
    double? Longitude,
    string? GeocodedLabel,
    bool? InOrNearSensitiveArea,
    string? SensitiveAreaName)
{
    public static CompanySiteDto From(CompanySite s) =>
        new(s.Id, s.Name, s.Address, s.Tenure, s.IsGeolocated, s.Latitude, s.Longitude, s.GeocodedLabel, s.InOrNearSensitiveArea, s.SensitiveAreaName);
}
