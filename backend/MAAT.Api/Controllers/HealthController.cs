using MAAT.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MAAT.Api.Controllers;

// docs/specs/deploiement.md, section 6 : point de contrôle de santé public, sans
// authentification, utilisé par le script de déploiement et la supervision. Ne renvoie
// ni version ni détail d'infrastructure (section 6) — juste une indication de
// disponibilité, y compris quand la base est indisponible (cas de test 9).
[ApiController]
[Route("api/health")]
public class HealthController(IDatabaseHealthCheck databaseHealthCheck) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        bool databaseReachable;
        try
        {
            databaseReachable = await databaseHealthCheck.CanConnectAsync(ct);
        }
        catch
        {
            databaseReachable = false;
        }

        if (!databaseReachable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unhealthy" });
        }

        return Ok(new { status = "healthy" });
    }
}
