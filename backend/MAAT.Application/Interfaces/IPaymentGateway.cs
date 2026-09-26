using MAAT.Domain.Enums;

namespace MAAT.Application.Interfaces;

// docs/specs/abonnement.md, section 4 : tout ce que MAAT demande au prestataire de paiement.
// Implémenté par StripePaymentGateway (MAAT.Infrastructure) ; rien de propre à Stripe ne
// traverse cette interface, pour que l'Application reste testable avec un double.
public interface IPaymentGateway
{
    // Page de paiement hébergée par le prestataire : renvoie l'URL vers laquelle rediriger.
    // Aucune donnée de carte ne transite par MAAT.
    Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken ct);

    // Portail client du prestataire : changement d'offre, moyen de paiement, factures,
    // résiliation.
    Task<string> CreateCustomerPortalSessionAsync(string customerId, string returnPath, CancellationToken ct);

    // Vérifie la signature du webhook puis relit l'abonnement concerné chez le prestataire.
    // Renvoie null pour un événement sans effet sur l'abonnement. Lève
    // InvalidWebhookSignatureException si la signature ne correspond pas.
    Task<ProviderSubscriptionSnapshot?> ReadWebhookAsync(string payload, string signatureHeader, CancellationToken ct);

    // Session de paiement relue chez le prestataire au retour du navigateur. Renvoie null si
    // elle est inconnue ou pas encore payée. L'identifiant vient de l'URL de retour, donc du
    // client : c'est la réponse du prestataire qui fait foi, jamais l'identifiant lui-même.
    Task<ProviderSubscriptionSnapshot?> ReadCheckoutSessionAsync(string checkoutSessionId, CancellationToken ct);

    // État courant d'un abonnement connu (retour du portail client). Null s'il n'est pas
    // exploitable (prix hors catalogue, sans entreprise MAAT).
    Task<ProviderSubscriptionSnapshot?> ReadSubscriptionAsync(string subscriptionId, CancellationToken ct);

    // Résiliation immédiate, sans attendre la fin de période : suppression du compte (RGPD).
    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct);
}

// Chemins relatifs à l'application web : l'adaptateur les préfixe par son URL publique.
public sealed record CheckoutSessionRequest(
    Guid CompanyId,
    string CustomerEmail,
    string? ExistingCustomerId,
    SubscriptionPlan Plan,
    BillingPeriod Period,
    string SuccessPath,
    string CancelPath);

public sealed record ProviderSubscriptionSnapshot(
    Guid CompanyId,
    string CustomerId,
    string SubscriptionId,
    SubscriptionPlan Plan,
    BillingPeriod Period,
    ProviderSubscriptionState State);
