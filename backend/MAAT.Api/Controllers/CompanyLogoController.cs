using MAAT.Application.Exceptions;
using MAAT.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAAT.Api.Controllers;

// docs/specs/rapport-pdf.md, section 7 : logo de l'entreprise du principal authentifié. Jamais
// d'URL publique (CLAUDE.md, hébergement) : l'image n'est servie que par cet endpoint
// authentifié, et l'écran la récupère en blob, comme le rapport lui-même.
[ApiController]
[Route("api/company/logo")]
[Authorize]
public class CompanyLogoController(CompanyLogoService logoService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var png = await logoService.GetAsync(ct);
        if (png is null)
        {
            return NotFound();
        }

        // no-store : l'image d'une entreprise ne doit pas rester dans un cache partagé, et un
        // logo remplacé doit s'afficher aussitôt.
        Response.Headers.CacheControl = "no-store";
        return File(png, "image/png");
    }

    // Envoyer ou retirer le logo engage l'identité de l'entreprise sur le document qu'elle
    // transmet à des tiers : réservé à ses administrateurs, comme le choix de l'offre.
    // RequestSizeLimit : un peu plus que la limite du fichier, pour l'enveloppe multipart.
    // Au-delà, Kestrel coupe la requête avant même de la lire en mémoire.
    [HttpPut]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(CompanyLogoService.MaxUploadBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            return BadRequest(new { message = "Aucun fichier reçu." });
        }

        if (file.Length > CompanyLogoService.MaxUploadBytes)
        {
            return BadRequest(new { message = "Le logo ne doit pas dépasser 2 Mo." });
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);

        try
        {
            await logoService.UploadAsync(buffer.ToArray(), ct);
        }
        catch (InvalidLogoImageException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return NoContent();
    }

    [HttpDelete]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        await logoService.DeleteAsync(ct);
        return NoContent();
    }
}
