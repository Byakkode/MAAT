using System.Net;
using System.Text.Json;
using MAAT.Application.Exceptions;
using MAAT.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace MAAT.Infrastructure.Billing;

// docs/specs/abonnement.md, section 4 : seul fichier du backend qui parle à Stripe.
public sealed class StripePaymentGateway(
    IOptions<StripeOptions> options,
    ILogger<StripePaymentGateway> logger) : IPaymentGateway
{
    public async Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken ct)
    {
        var client = CreateClient();
        var lookupKey = StripeCatalog.LookupKey(request.Plan, request.Period);
        var companyMetadata = new Dictionary<string, string>
        {
            [StripeCatalog.CompanyIdMetadataKey] = request.CompanyId.ToString(),
        };

        try
        {
            var prices = await new PriceService(client).ListAsync(
                new PriceListOptions { LookupKeys = [lookupKey], Active = true },
                cancellationToken: ct);
            var price = prices.Data.FirstOrDefault()
                ?? throw new PaymentProviderException($"Aucun prix Stripe actif pour la clé de recherche « {lookupKey} ».");

            var session = await new SessionService(client).CreateAsync(
                new SessionCreateOptions
                {
                    Mode = "subscription",
                    LineItems = [new SessionLineItemOptions { Price = price.Id, Quantity = 1 }],
                    ClientReferenceId = request.CompanyId.ToString(),
                    // Client Stripe réutilisé s'il existe déjà (ancien abonnement résilié) :
                    // ses factures restent regroupées au même endroit.
                    Customer = request.ExistingCustomerId,
                    CustomerEmail = request.ExistingCustomerId is null ? request.CustomerEmail : null,
                    CustomerUpdate = request.ExistingCustomerId is null
                        ? null
                        : new SessionCustomerUpdateOptions { Address = "auto", Name = "auto" },
                    // Prix affichés HT : Stripe Tax ajoute la TVA d'après l'adresse de
                    // facturation, et applique l'autoliquidation si un numéro de TVA
                    // intracommunautaire valide est saisi.
                    BillingAddressCollection = "required",
                    AutomaticTax = new SessionAutomaticTaxOptions { Enabled = true },
                    TaxIdCollection = new SessionTaxIdCollectionOptions { Enabled = true },
                    Locale = "fr",
                    Metadata = companyMetadata,
                    SubscriptionData = new SessionSubscriptionDataOptions { Metadata = companyMetadata },
                    // {CHECKOUT_SESSION_ID} est remplacé par Stripe au moment de la redirection :
                    // la page de confirmation le renvoie à l'API, qui relit la session chez
                    // Stripe pour activer l'offre sans attendre le webhook.
                    SuccessUrl = ReturnUrl(request.SuccessPath) + "?session_id={CHECKOUT_SESSION_ID}",
                    CancelUrl = ReturnUrl(request.CancelPath),
                },
                cancellationToken: ct);

            return session.Url;
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Création de la session de paiement Stripe refusée ({StripeError}).", ex.StripeError?.Code);
            throw new PaymentProviderException("Impossible d'ouvrir la page de paiement.", ex);
        }
    }

    public async Task<string> CreateCustomerPortalSessionAsync(string customerId, string returnPath, CancellationToken ct)
    {
        var client = CreateClient();
        try
        {
            var session = await new Stripe.BillingPortal.SessionService(client).CreateAsync(
                new Stripe.BillingPortal.SessionCreateOptions
                {
                    Customer = customerId,
                    ReturnUrl = ReturnUrl(returnPath),
                    Locale = "fr",
                },
                cancellationToken: ct);

            return session.Url;
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Ouverture du portail client Stripe refusée ({StripeError}).", ex.StripeError?.Code);
            throw new PaymentProviderException("Impossible d'ouvrir la gestion de l'abonnement.", ex);
        }
    }

    public async Task<ProviderSubscriptionSnapshot?> ReadWebhookAsync(string payload, string signatureHeader, CancellationToken ct)
    {
        var webhookSecret = options.Value.WebhookSecret;
        if (string.IsNullOrEmpty(webhookSecret))
        {
            throw new PaymentProviderNotConfiguredException();
        }

        try
        {
            EventUtility.ValidateSignature(payload, signatureHeader, webhookSecret);
        }
        catch (StripeException)
        {
            throw new InvalidWebhookSignatureException();
        }

        // L'événement ne sert qu'à savoir QUEL abonnement a changé : son état est relu
        // ci-dessous chez Stripe. Deux raisons. Les webhooks peuvent arriver en retard ou
        // dans le désordre — l'état relu est toujours le plus récent. Et le contenu d'un
        // événement suit la version d'API configurée sur l'endpoint dans le tableau de bord,
        // pas celle du SDK : lire seulement l'identifiant dans le JSON brut évite de
        // dépendre de cette configuration.
        var subscriptionId = ExtractSubscriptionId(payload);
        return subscriptionId is null ? null : await ReadSubscriptionAsync(subscriptionId, ct);
    }

    public async Task<ProviderSubscriptionSnapshot?> ReadCheckoutSessionAsync(string checkoutSessionId, CancellationToken ct)
    {
        // Valeur venue de l'URL de retour : tout ce qui n'a pas la forme d'une session Checkout
        // est écarté avant d'interroger Stripe.
        if (!checkoutSessionId.StartsWith("cs_", StringComparison.Ordinal) || checkoutSessionId.Length > 255)
        {
            return null;
        }

        var client = CreateClient();
        Session session;
        try
        {
            session = await new SessionService(client).GetAsync(checkoutSessionId, cancellationToken: ct);
        }
        catch (StripeException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // Identifiant inventé ou d'un autre compte Stripe : rien à confirmer.
            return null;
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Lecture de la session de paiement Stripe impossible.");
            throw new PaymentProviderException("Session de paiement Stripe illisible.", ex);
        }

        // « complete » : Stripe a encaissé le premier paiement et créé l'abonnement. Tant que ce
        // n'est pas le cas (session abandonnée, 3-D Secure en cours), il n'y a rien à activer.
        return session.Status == "complete" && session.SubscriptionId is not null
            ? await ReadSubscriptionAsync(session.SubscriptionId, ct)
            : null;
    }

    public async Task<ProviderSubscriptionSnapshot?> ReadSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        var client = CreateClient();
        Subscription subscription;
        try
        {
            subscription = await new SubscriptionService(client).GetAsync(subscriptionId, cancellationToken: ct);
        }
        catch (StripeException ex)
        {
            // Relancé : pour un webhook, Stripe le rejouera (réponse non 2xx) jusqu'à ce que la
            // lecture réussisse.
            logger.LogError(ex, "Lecture de l'abonnement Stripe {SubscriptionId} impossible.", subscriptionId);
            throw new PaymentProviderException("Abonnement Stripe illisible.", ex);
        }

        if (!subscription.Metadata.TryGetValue(StripeCatalog.CompanyIdMetadataKey, out var companyIdValue)
            || !Guid.TryParse(companyIdValue, out var companyId))
        {
            logger.LogWarning("Abonnement Stripe {SubscriptionId} sans entreprise MAAT associée : ignoré.", subscriptionId);
            return null;
        }

        var lookupKey = subscription.Items.Data.FirstOrDefault()?.Price?.LookupKey;
        if (!StripeCatalog.TryParseLookupKey(lookupKey, out var plan, out var period))
        {
            logger.LogWarning("Abonnement Stripe {SubscriptionId} sur un prix inconnu ({LookupKey}) : ignoré.", subscriptionId, lookupKey);
            return null;
        }

        var state = StripeCatalog.MapStatus(subscription.Status);
        if (state is null)
        {
            logger.LogWarning("Statut d'abonnement Stripe inconnu « {Status} » : ignoré.", subscription.Status);
            return null;
        }

        return new ProviderSubscriptionSnapshot(companyId, subscription.CustomerId, subscription.Id, plan, period, state.Value);
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        var client = CreateClient();
        try
        {
            await new SubscriptionService(client).CancelAsync(subscriptionId, cancellationToken: ct);
        }
        catch (StripeException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // Déjà supprimé côté Stripe : l'objectif (ne plus facturer) est atteint.
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Résiliation de l'abonnement Stripe {SubscriptionId} refusée.", subscriptionId);
            throw new PaymentProviderException("Impossible de résilier l'abonnement en cours.", ex);
        }
    }

    // Événements qui concernent un abonnement ; tous les autres sont acquittés sans effet.
    // checkout.session.completed porte l'identifiant dans « subscription », les
    // customer.subscription.* dans « id ».
    public static string? ExtractSubscriptionId(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var type = root.GetProperty("type").GetString();
        var data = root.GetProperty("data").GetProperty("object");

        var property = type switch
        {
            EventTypes.CheckoutSessionCompleted => "subscription",
            EventTypes.CustomerSubscriptionCreated
                or EventTypes.CustomerSubscriptionUpdated
                or EventTypes.CustomerSubscriptionDeleted
                or EventTypes.CustomerSubscriptionPaused
                or EventTypes.CustomerSubscriptionResumed => "id",
            _ => null,
        };

        return property is not null
            && data.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private StripeClient CreateClient()
    {
        var secretKey = options.Value.SecretKey;
        if (string.IsNullOrEmpty(secretKey))
        {
            throw new PaymentProviderNotConfiguredException();
        }

        return new StripeClient(secretKey);
    }

    private string ReturnUrl(string path) => options.Value.ReturnBaseUrl.TrimEnd('/') + path;
}
