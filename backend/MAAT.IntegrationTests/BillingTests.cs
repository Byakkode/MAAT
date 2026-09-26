using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/abonnement.md, section 8 : cas de test de l'abonnement, de l'inscription au
// webhook, avec FakePaymentGateway à la place de Stripe (BillingApiFixture).
[Collection(BillingApiCollection.Name)]
public class BillingTests(BillingApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";

    private static string UniqueEmail() => $"billing-{Guid.NewGuid():N}@example.test";

    private static object RegisterPayload(string email, string? plan = null, string? billingPeriod = null) => new
    {
        email,
        password = ValidPassword,
        companyName = "Entreprise Abonnement",
        sectorCode = "6201Z",
        sizeRange = "Micro",
        region = "Île-de-France",
        plan,
        billingPeriod,
    };

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        // Corps JSON posé même vide, comme le fait le client web (httpClient.ts) : un POST
        // sans Content-Type serait rejeté en 415 avant d'atteindre le contrôleur.
        request.Content = JsonContent.Create(body ?? new { });
        return request;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private static Guid CompanyIdOf(string accessToken) =>
        Guid.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(accessToken).Claims.Single(c => c.Type == "company_id").Value);

    private async Task<(string AccessToken, Guid CompanyId)> RegisterAndLoginAsync(HttpClient client, string? plan = null, string? billingPeriod = null)
    {
        var email = UniqueEmail();
        var register = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, plan, billingPeriod));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var accessToken = await LoginAsync(client, email);
        return (accessToken, CompanyIdOf(accessToken));
    }

    private static async Task<JsonElement> GetSubscriptionAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/billing/subscription");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpResponseMessage> SendWebhookAsync(HttpClient client, ProviderSubscriptionSnapshot? snapshot)
    {
        var signature = $"sig-{Guid.NewGuid():N}";
        fixture.PaymentGateway.RegisterWebhook(signature, snapshot);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhook")
        {
            Content = new StringContent("{\"type\":\"customer.subscription.updated\"}", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", signature);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Cas1_CompteSansOffre_StatutNul()
    {
        var client = fixture.CreateClient();
        var (accessToken, _) = await RegisterAndLoginAsync(client);

        var subscription = await GetSubscriptionAsync(client, accessToken);

        Assert.Equal(JsonValueKind.Null, subscription.GetProperty("status").ValueKind);
        Assert.Equal(JsonValueKind.Null, subscription.GetProperty("plan").ValueKind);
    }

    [Fact]
    public async Task Cas2_InscriptionAvecStarter_ActiveImmediatement()
    {
        var client = fixture.CreateClient();
        var (accessToken, _) = await RegisterAndLoginAsync(client, "Starter");

        var subscription = await GetSubscriptionAsync(client, accessToken);

        Assert.Equal("Starter", subscription.GetProperty("plan").GetString());
        Assert.Equal("Active", subscription.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cas3_InscriptionAvecOffrePayante_EnAttenteDePaiement()
    {
        var client = fixture.CreateClient();
        var (accessToken, _) = await RegisterAndLoginAsync(client, "Essential", "Yearly");

        var subscription = await GetSubscriptionAsync(client, accessToken);

        Assert.Equal("Essential", subscription.GetProperty("plan").GetString());
        Assert.Equal("Yearly", subscription.GetProperty("billingPeriod").GetString());
        Assert.Equal("PendingPayment", subscription.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cas4_InscriptionAvecEnterprise_RefuseeSansCreerDeCompte()
    {
        var client = fixture.CreateClient();
        var email = UniqueEmail();

        var register = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, "Enterprise"));

        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Cas5_ChoixDeStarter_ActiveStarter()
    {
        var client = fixture.CreateClient();
        var (accessToken, _) = await RegisterAndLoginAsync(client, "Professional");

        var response = await client.SendAsync(Authorized(HttpMethod.Post, "/api/billing/starter", accessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var subscription = await GetSubscriptionAsync(client, accessToken);
        Assert.Equal("Starter", subscription.GetProperty("plan").GetString());
        Assert.Equal("Active", subscription.GetProperty("status").GetString());
    }

    // L'entreprise vient du jeton, jamais du corps de la requête ; démarrer un paiement ne
    // change rien à l'abonnement tant que le webhook n'a pas confirmé le paiement.
    [Fact]
    public async Task Cas6_Paiement_SessionOuverteSansActiverLOffre()
    {
        var client = fixture.CreateClient();
        var (accessToken, companyId) = await RegisterAndLoginAsync(client);

        var response = await client.SendAsync(Authorized(HttpMethod.Post, "/api/billing/checkout", accessToken,
            new { plan = "Essential", billingPeriod = "Monthly" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(TestDoubles.FakePaymentGateway.CheckoutUrl, body.GetProperty("url").GetString());

        var checkout = fixture.PaymentGateway.CheckoutRequests.Single(r => r.CompanyId == companyId);
        Assert.Equal(SubscriptionPlan.Essential, checkout.Plan);
        Assert.Equal("/abonnement/confirmation", checkout.SuccessPath);

        var subscription = await GetSubscriptionAsync(client, accessToken);
        Assert.Equal(JsonValueKind.Null, subscription.GetProperty("status").ValueKind);
    }

    [Theory]
    [InlineData("Enterprise")]
    [InlineData("Starter")]
    public async Task Cas7_Paiement_OffreNonAchetable_Refuse(string plan)
    {
        var client = fixture.CreateClient();
        var (accessToken, _) = await RegisterAndLoginAsync(client);

        var response = await client.SendAsync(Authorized(HttpMethod.Post, "/api/billing/checkout", accessToken,
            new { plan, billingPeriod = "Monthly" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Choisir ou payer une offre engage l'entreprise : réservé à ses administrateurs.
    [Fact]
    public async Task Cas8_NonAdministrateur_NePeutNiChoisirNiPayer()
    {
        var client = fixture.CreateClient();
        var (_, companyId) = await RegisterAndLoginAsync(client);

        var viewerEmail = UniqueEmail();
        await using (var context = fixture.CreateDbContext())
        {
            context.Users.Add(new User(viewerEmail, new BcryptPasswordHasher().Hash(ValidPassword), companyId, UserRole.Viewer));
            await context.SaveChangesAsync();
        }

        var viewerToken = await LoginAsync(client, viewerEmail);

        var checkout = await client.SendAsync(Authorized(HttpMethod.Post, "/api/billing/checkout", viewerToken,
            new { plan = "Essential", billingPeriod = "Monthly" }));
        var starter = await client.SendAsync(Authorized(HttpMethod.Post, "/api/billing/starter", viewerToken));

        Assert.Equal(HttpStatusCode.Forbidden, checkout.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, starter.StatusCode);
        await GetSubscriptionAsync(client, viewerToken);
    }

    [Fact]
    public async Task Cas9_Webhook_SignatureInvalide_Rejete()
    {
        var client = fixture.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhook")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", "signature-inconnue");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cas10_Webhook_PaiementRecu_ActiveLOffreEtOuvreLePortail()
    {
        var client = fixture.CreateClient();
        var (accessToken, companyId) = await RegisterAndLoginAsync(client);
        var subscriptionId = $"sub_{Guid.NewGuid():N}";

        var webhook = await SendWebhookAsync(client, new ProviderSubscriptionSnapshot(
            companyId, "cus_cas10", subscriptionId, SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Active));

        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        var subscription = await GetSubscriptionAsync(client, accessToken);
        Assert.Equal("Essential", subscription.GetProperty("plan").GetString());
        Assert.Equal("Active", subscription.GetProperty("status").GetString());
        Assert.True(subscription.GetProperty("hasBillingAccount").GetBoolean());

        var portal = await client.SendAsync(Authorized(HttpMethod.Post, "/api/billing/portal", accessToken));
        Assert.Equal(HttpStatusCode.OK, portal.StatusCode);
        Assert.Contains("cus_cas10", fixture.PaymentGateway.PortalCustomers);

        // Un seul abonnement payant par entreprise : ni second paiement, ni retour à Starter
        // sans passer par le portail.
        var secondCheckout = await client.SendAsync(Authorized(HttpMethod.Post, "/api/billing/checkout", accessToken,
            new { plan = "Professional", billingPeriod = "Monthly" }));
        var starter = await client.SendAsync(Authorized(HttpMethod.Post, "/api/billing/starter", accessToken));
        Assert.Equal(HttpStatusCode.Conflict, secondCheckout.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, starter.StatusCode);
    }

    [Fact]
    public async Task Cas11_Webhook_AbonnementTermine_RetourAStarter()
    {
        var client = fixture.CreateClient();
        var (accessToken, companyId) = await RegisterAndLoginAsync(client, "Essential", "Monthly");
        var subscriptionId = $"sub_{Guid.NewGuid():N}";

        await SendWebhookAsync(client, new ProviderSubscriptionSnapshot(
            companyId, "cus_cas11", subscriptionId, SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Active));
        var ended = await SendWebhookAsync(client, new ProviderSubscriptionSnapshot(
            companyId, "cus_cas11", subscriptionId, SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Ended));

        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        var subscription = await GetSubscriptionAsync(client, accessToken);
        Assert.Equal("Starter", subscription.GetProperty("plan").GetString());
        Assert.Equal("Active", subscription.GetProperty("status").GetString());
    }

    // Stripe rejoue un webhook tant qu'il ne reçoit pas de 2xx : un événement sans effet
    // (entreprise inconnue, type ignoré) doit être acquitté, pas rejeté.
    [Fact]
    public async Task Cas12_Webhook_EntrepriseInconnueOuEvenementIgnore_Acquitte()
    {
        var client = fixture.CreateClient();

        var unknownCompany = await SendWebhookAsync(client, new ProviderSubscriptionSnapshot(
            Guid.NewGuid(), "cus_x", "sub_x", SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Active));
        var ignored = await SendWebhookAsync(client, null);

        Assert.Equal(HttpStatusCode.OK, unknownCompany.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ignored.StatusCode);
    }

    // Retour de la page de paiement : l'offre est activée sans attendre le webhook, à partir de
    // la session relue chez Stripe (docs/specs/abonnement.md, section 5).
    [Fact]
    public async Task Cas16_RetourDePaiement_SessionPayee_ActiveSansWebhook()
    {
        var client = fixture.CreateClient();
        var (accessToken, companyId) = await RegisterAndLoginAsync(client, "Essential", "Monthly");
        var sessionId = $"cs_{Guid.NewGuid():N}";
        fixture.PaymentGateway.RegisterPaidCheckoutSession(sessionId, new ProviderSubscriptionSnapshot(
            companyId, "cus_cas16", $"sub_{Guid.NewGuid():N}", SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Active));

        var response = await client.SendAsync(Authorized(HttpMethod.Post, "/api/billing/checkout/confirm", accessToken, new { sessionId }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var subscription = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Essential", subscription.GetProperty("plan").GetString());
        Assert.Equal("Active", subscription.GetProperty("status").GetString());
    }

    // L'identifiant de session vient de l'URL, donc du client : une session inconnue ou non
    // payée n'active rien, celle d'une autre entreprise est refusée sans rien modifier.
    [Fact]
    public async Task Cas17_RetourDePaiement_SessionInconnueOuDUneAutreEntreprise_NActiveRien()
    {
        var client = fixture.CreateClient();
        var (accessToken, _) = await RegisterAndLoginAsync(client, "Essential", "Monthly");
        var otherCompanySession = $"cs_{Guid.NewGuid():N}";
        fixture.PaymentGateway.RegisterPaidCheckoutSession(otherCompanySession, new ProviderSubscriptionSnapshot(
            Guid.NewGuid(), "cus_autre", $"sub_{Guid.NewGuid():N}", SubscriptionPlan.Professional, BillingPeriod.Yearly, ProviderSubscriptionState.Active));

        var unknown = await client.SendAsync(Authorized(HttpMethod.Post, "/api/billing/checkout/confirm", accessToken, new { sessionId = "cs_inventee" }));
        var foreign = await client.SendAsync(Authorized(HttpMethod.Post, "/api/billing/checkout/confirm", accessToken, new { sessionId = otherCompanySession }));

        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal("PendingPayment", (await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        var subscription = await GetSubscriptionAsync(client, accessToken);
        Assert.Equal("PendingPayment", subscription.GetProperty("status").GetString());
    }

    // Retour du portail client après une résiliation : l'espace du compte affiche Starter sans
    // attendre le webhook.
    [Fact]
    public async Task Cas18_RetourDuPortail_EtatRelu_ResiliationVisibleSansWebhook()
    {
        var client = fixture.CreateClient();
        var (accessToken, companyId) = await RegisterAndLoginAsync(client);
        var subscriptionId = $"sub_{Guid.NewGuid():N}";
        await SendWebhookAsync(client, new ProviderSubscriptionSnapshot(
            companyId, "cus_cas18", subscriptionId, SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Active));
        fixture.PaymentGateway.SetSubscriptionState(new ProviderSubscriptionSnapshot(
            companyId, "cus_cas18", subscriptionId, SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Ended));

        var response = await client.SendAsync(Authorized(HttpMethod.Post, "/api/billing/refresh", accessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var subscription = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Starter", subscription.GetProperty("plan").GetString());
        Assert.Equal("Active", subscription.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cas13_Portail_SansAbonnementPayant_Refuse()
    {
        var client = fixture.CreateClient();
        var (accessToken, _) = await RegisterAndLoginAsync(client, "Starter");

        var response = await client.SendAsync(Authorized(HttpMethod.Post, "/api/billing/portal", accessToken));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // docs/specs/abonnement.md, section 6 : supprimer l'entreprise résilie son abonnement
    // payant, sinon le prélèvement continuerait pour une entreprise qui n'existe plus.
    [Fact]
    public async Task Cas14_SuppressionDuCompte_ResilieLAbonnementPayant()
    {
        var client = fixture.CreateClient();
        var (accessToken, companyId) = await RegisterAndLoginAsync(client);
        var subscriptionId = $"sub_{Guid.NewGuid():N}";
        await SendWebhookAsync(client, new ProviderSubscriptionSnapshot(
            companyId, "cus_cas14", subscriptionId, SubscriptionPlan.Professional, BillingPeriod.Yearly, ProviderSubscriptionState.Active));

        var delete = await client.SendAsync(Authorized(HttpMethod.Delete, "/api/me", accessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Contains(subscriptionId, fixture.PaymentGateway.CanceledSubscriptions);
        await using var context = fixture.CreateDbContext();
        Assert.False(await context.Companies.AnyAsync(c => c.Id == companyId));
        Assert.False(await context.Subscriptions.AnyAsync(s => s.CompanyId == companyId));
    }

    [Fact]
    public async Task Cas15_SuppressionDuCompte_ResiliationImpossible_RienNestSupprime()
    {
        var client = fixture.CreateClient();
        var (accessToken, companyId) = await RegisterAndLoginAsync(client);
        await SendWebhookAsync(client, new ProviderSubscriptionSnapshot(
            companyId, "cus_cas15", $"sub_{Guid.NewGuid():N}", SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Active));

        fixture.PaymentGateway.FailCancellation = true;
        try
        {
            var delete = await client.SendAsync(Authorized(HttpMethod.Delete, "/api/me", accessToken, new { password = ValidPassword }));

            Assert.Equal(HttpStatusCode.BadGateway, delete.StatusCode);
        }
        finally
        {
            fixture.PaymentGateway.FailCancellation = false;
        }

        await using var context = fixture.CreateDbContext();
        Assert.True(await context.Companies.AnyAsync(c => c.Id == companyId));
    }
}
