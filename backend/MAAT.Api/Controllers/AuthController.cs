using MAAT.Application.DTOs;
using MAAT.Application.Exceptions;
using MAAT.Application.Interfaces;
using MAAT.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MAAT.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, IUserRepository userRepository, ILogger<AuthController> logger) : ControllerBase
{
    private const string RefreshCookieName = "refresh_token";
    private const string CookiePath = "/api/auth";

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        try
        {
            await authService.RegisterAsync(request, ct);
        }
        catch (WeakPasswordException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (CompromisedPasswordException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        // Réponse identique, que l'adresse soit déjà enregistrée ou non (anti-énumération).
        return StatusCode(StatusCodes.Status201Created, new { message = "Vérifiez votre boîte mail pour confirmer votre inscription." });
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        try
        {
            var tokens = await authService.LoginAsync(request, ct);
            SetRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
            return Ok(new { accessToken = tokens.AccessToken, expiresAt = tokens.AccessTokenExpiresAt });
        }
        catch (InvalidCredentialsException ex)
        {
            // Section 5 : IP + horodatage (implicite au journal), jamais l'adresse testée —
            // `request.Email` n'apparaît délibérément pas dans ce message.
            logger.LogWarning(
                "Tentative de connexion échouée depuis {IpAddress}.",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized();
        }

        try
        {
            var tokens = await authService.RefreshAsync(refreshToken, ct);
            SetRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
            return Ok(new { accessToken = tokens.AccessToken, expiresAt = tokens.AccessTokenExpiresAt });
        }
        catch (RefreshTokenReuseDetectedException ex)
        {
            DeleteRefreshCookie();
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidRefreshTokenException ex)
        {
            DeleteRefreshCookie();
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) && !string.IsNullOrEmpty(refreshToken))
        {
            await authService.LogoutAsync(refreshToken, ct);
        }

        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        try
        {
            await authService.VerifyEmailAsync(request.Token, ct);
            return NoContent();
        }
        catch (InvalidVerificationTokenException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // docs/specs/rapport-pdf.md, section 6 : le frontend a besoin de savoir si l'adresse est
    // vérifiée pour désactiver le bouton de téléchargement du rapport avant même la première
    // tentative — emailVerified n'est délibérément pas un claim JWT (section 2 : "rien
    // d'autre" que sub/company_id/role/exp/iat/jti), donc lu ici depuis la base.
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var user = await userRepository.GetByIdAsync(userId, ct);

        return Ok(new
        {
            userId = User.FindFirst("sub")?.Value,
            companyId = User.FindFirst("company_id")?.Value,
            role = User.FindFirst("role")?.Value,
            email = user?.Email,
            emailVerified = user?.EmailVerified ?? false,
        });
    }

    // docs/specs/coquille-et-compte.md, section 4 : le bandeau d'adresse non vérifiée dépend de
    // cet endpoint. Anonyme et anti-énumération, même principe que Register ci-dessus : réponse
    // identique que l'adresse existe, soit déjà vérifiée, ou non — le frontend l'appelle depuis
    // une session authentifiée avec l'adresse lue sur Me(), mais l'endpoint lui-même ne le
    // suppose pas et ne l'exige pas.
    [HttpPost("resend-verification")]
    [EnableRateLimiting("resend-verification")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request, CancellationToken ct)
    {
        await authService.ResendVerificationEmailAsync(request.Email, ct);

        return StatusCode(StatusCodes.Status202Accepted, new
        {
            message = "Si un compte existe pour cette adresse et n'est pas encore vérifié, un nouvel e-mail de vérification vient d'être envoyé.",
        });
    }

    private void SetRefreshCookie(string token, DateTimeOffset expiresAt)
    {
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
            Path = CookiePath,
        });
    }

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = CookiePath });
}
