using MAAT.Application.DTOs;
using MAAT.Application.Exceptions;
using MAAT.Application.UseCases;
using MAAT.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAAT.Api.Controllers;

// docs/specs/abonnement.md, section 2. Lecture ouverte à tout membre de l'entreprise ; choisir
// ou payer une offre engage l'entreprise, donc réservé à ses administrateurs.
[ApiController]
[Route("api/billing")]
[Authorize]
public class BillingController(
    BillingService billingService,
    BillingWebhookHandler webhookHandler,
    ILogger<BillingController> logger) : ControllerBase
{
    [HttpGet("subscription")]
    public async Task<ActionResult<SubscriptionView>> GetSubscription(CancellationToken ct) =>
        await billingService.GetCurrentAsync(ct);

    [HttpPost("starter")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChooseStarter(CancellationToken ct)
    {
        try
        {
            return Ok(await billingService.ChooseStarterAsync(ct));
        }
        catch (PaidSubscriptionActiveException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("checkout")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> StartCheckout(StartCheckoutRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await billingService.StartCheckoutAsync(request, ct));
        }
        catch (PlanNotAvailableException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (PaidSubscriptionActiveException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (PaymentProviderNotConfiguredException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (PaymentProviderException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    // Retour de la page de paiement (docs/specs/abonnement.md, section 5) : ouvert à tout membre
    // de l'entreprise, puisqu'il ne fait que relire chez Stripe un paiement déjà effectué.
    [HttpPost("checkout/confirm")]
    public async Task<IActionResult> ConfirmCheckout(ConfirmCheckoutRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await billingService.ConfirmCheckoutAsync(request.SessionId, ct));
        }
        catch (CheckoutSessionNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (PaymentProviderNotConfiguredException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (PaymentProviderException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    // Retour du portail client : relit l'abonnement chez Stripe (même principe).
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        try
        {
            return Ok(await billingService.RefreshFromProviderAsync(ct));
        }
        catch (PaymentProviderNotConfiguredException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (PaymentProviderException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    [HttpPost("portal")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> OpenPortal(CancellationToken ct)
    {
        try
        {
            return Ok(await billingService.OpenCustomerPortalAsync(ct));
        }
        catch (NoBillingAccountException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (PaymentProviderNotConfiguredException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (PaymentProviderException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    // Appelé par Stripe, pas par le navigateur : anonyme, authentifié par la signature de
    // l'en-tête Stripe-Signature, vérifiée sur le corps brut (d'où la lecture manuelle du
    // flux plutôt qu'un paramètre [FromBody], qui le re-sérialiserait). Toute réponse hors
    // 2xx fait rejouer l'événement par Stripe, pendant trois jours.
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        try
        {
            await webhookHandler.HandleAsync(payload, signature, ct);
            return Ok();
        }
        catch (InvalidWebhookSignatureException)
        {
            logger.LogWarning(
                "Webhook de paiement à la signature invalide depuis {IpAddress}.",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            return BadRequest();
        }
        catch (PaymentProviderNotConfiguredException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
