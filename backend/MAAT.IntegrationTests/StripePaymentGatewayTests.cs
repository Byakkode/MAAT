using System.Security.Cryptography;
using System.Text;
using MAAT.Application.Exceptions;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Billing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MAAT.IntegrationTests;

// docs/specs/abonnement.md, section 8 : ce qui, dans l'adaptateur Stripe, se vérifie sans
// réseau ni clé — la signature des webhooks et la correspondance avec le catalogue Stripe.
// Le reste (appels à l'API Stripe) se vérifie en mode test Stripe, pas en CI.
public class StripePaymentGatewayTests
{
    private const string WebhookSecret = "whsec_test_secret_for_unit_tests";

    private static StripePaymentGateway CreateGateway() =>
        new(Options.Create(new StripeOptions { WebhookSecret = WebhookSecret }), NullLogger<StripePaymentGateway>.Instance);

    // Même calcul que Stripe (https://docs.stripe.com/webhooks#verify-manually) : HMAC-SHA256
    // de « horodatage.corps » avec le secret de l'endpoint.
    private static string SignatureHeader(string payload, string secret, DateTimeOffset at)
    {
        var timestamp = at.ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }

    [Fact]
    public async Task Webhook_SignatureFaiteAvecUnAutreSecret_Rejete()
    {
        const string payload = """{"type":"customer.subscription.updated","data":{"object":{"id":"sub_1"}}}""";

        await Assert.ThrowsAsync<InvalidWebhookSignatureException>(() =>
            CreateGateway().ReadWebhookAsync(payload, SignatureHeader(payload, "whsec_autre", DateTimeOffset.UtcNow), CancellationToken.None));
    }

    [Fact]
    public async Task Webhook_CorpsModifieApresSignature_Rejete()
    {
        const string payload = """{"type":"customer.subscription.updated","data":{"object":{"id":"sub_1"}}}""";
        var header = SignatureHeader(payload, WebhookSecret, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidWebhookSignatureException>(() =>
            CreateGateway().ReadWebhookAsync(payload.Replace("sub_1", "sub_2"), header, CancellationToken.None));
    }

    // Protection contre le rejeu : une signature valide mais ancienne (tolérance Stripe par
    // défaut : 5 minutes) est refusée.
    [Fact]
    public async Task Webhook_SignatureTropAncienne_Rejete()
    {
        const string payload = """{"type":"customer.subscription.updated","data":{"object":{"id":"sub_1"}}}""";

        await Assert.ThrowsAsync<InvalidWebhookSignatureException>(() =>
            CreateGateway().ReadWebhookAsync(payload, SignatureHeader(payload, WebhookSecret, DateTimeOffset.UtcNow.AddHours(-1)), CancellationToken.None));
    }

    [Fact]
    public async Task Webhook_EvenementSansRapportAvecUnAbonnement_Ignore()
    {
        const string payload = """{"type":"invoice.paid","data":{"object":{"id":"in_1","subscription":"sub_1"}}}""";

        var snapshot = await CreateGateway().ReadWebhookAsync(payload, SignatureHeader(payload, WebhookSecret, DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.Null(snapshot);
    }

    // L'identifiant vient de l'URL de retour : ce qui n'a pas la forme d'une session Checkout
    // est écarté sans même interroger Stripe (aucune clé n'est configurée ici).
    [Theory]
    [InlineData("sub_123")]
    [InlineData("../v1/customers")]
    [InlineData("")]
    public async Task RetourDePaiement_IdentifiantMalForme_IgnoreSansAppelAStripe(string sessionId)
    {
        Assert.Null(await CreateGateway().ReadCheckoutSessionAsync(sessionId, CancellationToken.None));
    }

    [Theory]
    [InlineData("""{"type":"checkout.session.completed","data":{"object":{"id":"cs_1","subscription":"sub_42"}}}""", "sub_42")]
    [InlineData("""{"type":"customer.subscription.deleted","data":{"object":{"id":"sub_43"}}}""", "sub_43")]
    [InlineData("""{"type":"checkout.session.completed","data":{"object":{"id":"cs_2","subscription":null}}}""", null)]
    [InlineData("""{"type":"invoice.paid","data":{"object":{"id":"in_1"}}}""", null)]
    public void Webhook_IdentifiantDAbonnement_LuSelonLeType(string payload, string? expected)
    {
        Assert.Equal(expected, StripePaymentGateway.ExtractSubscriptionId(payload));
    }

    [Theory]
    [InlineData(SubscriptionPlan.Essential, BillingPeriod.Monthly, "essential_monthly")]
    [InlineData(SubscriptionPlan.Essential, BillingPeriod.Yearly, "essential_yearly")]
    [InlineData(SubscriptionPlan.Professional, BillingPeriod.Monthly, "professional_monthly")]
    [InlineData(SubscriptionPlan.Professional, BillingPeriod.Yearly, "professional_yearly")]
    public void CleDeRecherche_AllerRetour(SubscriptionPlan plan, BillingPeriod period, string lookupKey)
    {
        Assert.Equal(lookupKey, StripeCatalog.LookupKey(plan, period));
        Assert.True(StripeCatalog.TryParseLookupKey(lookupKey, out var parsedPlan, out var parsedPeriod));
        Assert.Equal(plan, parsedPlan);
        Assert.Equal(period, parsedPeriod);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("starter_monthly")]
    [InlineData("essential_weekly")]
    [InlineData("autre_produit")]
    [InlineData("essential")]
    public void CleDeRecherche_Inconnue_Refusee(string? lookupKey)
    {
        Assert.False(StripeCatalog.TryParseLookupKey(lookupKey, out _, out _));
    }

    [Theory]
    [InlineData("active", ProviderSubscriptionState.Active)]
    [InlineData("trialing", ProviderSubscriptionState.Active)]
    [InlineData("past_due", ProviderSubscriptionState.PastDue)]
    [InlineData("unpaid", ProviderSubscriptionState.PastDue)]
    [InlineData("paused", ProviderSubscriptionState.PastDue)]
    [InlineData("incomplete", ProviderSubscriptionState.Incomplete)]
    [InlineData("incomplete_expired", ProviderSubscriptionState.Ended)]
    [InlineData("canceled", ProviderSubscriptionState.Ended)]
    public void StatutStripe_Traduit(string status, ProviderSubscriptionState expected)
    {
        Assert.Equal(expected, StripeCatalog.MapStatus(status));
    }

    [Fact]
    public void StatutStripe_Inconnu_SansEffet()
    {
        Assert.Null(StripeCatalog.MapStatus("statut_futur"));
    }
}
