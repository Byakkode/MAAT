using MAAT.Application.DTOs;
using MAAT.Application.Exceptions;
using MAAT.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MAAT.Api.Controllers;

// docs/specs/auth-securite-rgpd.md, section 6 : droits RGPD portés par le compte
// authentifié (jamais par un identifiant transmis par le client — même principe que
// le cloisonnement d'entreprise, section 4).
[ApiController]
[Route("api/me")]
[Authorize]
public class AccountController(AccountService accountService, ILogger<AccountController> logger) : ControllerBase
{
    // POST, pas GET : un GET ne prend pas de corps côté navigateur (fetch() lève une
    // TypeError sur un GET avec body), et un mot de passe en en-tête ou en query string
    // finirait journalisé par les reverse proxies — interdit par la section 5.
    [HttpPost("export")]
    [EnableRateLimiting("password-confirmation")]
    public async Task<IActionResult> Export(PasswordConfirmationRequest request, CancellationToken ct)
    {
        try
        {
            var export = await accountService.ExportAsync(request.Password, ct);
            return Ok(export);
        }
        catch (PasswordConfirmationFailedException ex)
        {
            LogConfirmationFailure();
            return Unauthorized(new { message = ex.Message });
        }
    }

    // docs/specs/coquille-et-compte.md, section 5. Même politique de débit que
    // export/delete : accepte un mot de passe en clair et le vérifie, donc même oracle
    // potentiel sans limitation (section 5 de auth-securite-rgpd.md).
    [HttpPut("password")]
    [EnableRateLimiting("password-confirmation")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        Request.Cookies.TryGetValue("refresh_token", out var currentRefreshToken);

        try
        {
            await accountService.ChangePasswordAsync(request.CurrentPassword, request.NewPassword, currentRefreshToken, ct);
        }
        catch (PasswordConfirmationFailedException ex)
        {
            LogConfirmationFailure();
            return Unauthorized(new { message = ex.Message });
        }
        catch (WeakPasswordException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (CompromisedPasswordException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return NoContent();
    }

    [HttpDelete]
    [EnableRateLimiting("password-confirmation")]
    public async Task<IActionResult> Delete(PasswordConfirmationRequest request, CancellationToken ct)
    {
        try
        {
            await accountService.DeleteAccountAsync(request.Password, ct);
        }
        catch (PasswordConfirmationFailedException ex)
        {
            LogConfirmationFailure();
            return Unauthorized(new { message = ex.Message });
        }

        Response.Cookies.Delete("refresh_token", new CookieOptions { Path = "/api/auth" });
        return NoContent();
    }

    // Section 5 : IP + horodatage (implicite au journal), jamais le mot de passe testé
    // ni l'identité au-delà de ce que le journal d'accès porte déjà.
    private void LogConfirmationFailure() =>
        logger.LogWarning(
            "Échec de reconfirmation de mot de passe depuis {IpAddress}.",
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}
