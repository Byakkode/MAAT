namespace MAAT.Application.Exceptions;

// Clé secrète absente : la facturation est désactivée, le reste de l'application tourne
// (même principe que le jeton GitHub du support).
public sealed class PaymentProviderNotConfiguredException()
    : Exception("Le paiement en ligne est temporairement indisponible.");

// Le prestataire a refusé ou n'a pas répondu (réseau, clé révoquée, prix introuvable).
public sealed class PaymentProviderException(string message, Exception? inner = null)
    : Exception(message, inner);

public sealed class InvalidWebhookSignatureException()
    : Exception("Signature de webhook invalide.");

public sealed class NoBillingAccountException()
    : Exception("Aucun abonnement payant à gérer pour cette entreprise.");

// Session de paiement d'une autre entreprise : répondu comme une session inconnue, pour ne pas
// confirmer son existence.
public sealed class CheckoutSessionNotFoundException()
    : Exception("Session de paiement introuvable.");

// docs/specs/abonnement.md, section 8 : action hors de l'offre effective de l'entreprise.
// Traduite en 403 { code: "plan_required", requiredPlan } par PlanRequiredExceptionFilter,
// pour que l'écran propose l'offre supérieure au lieu d'annoncer un manque de rôle.
public sealed class PlanRequiredException(MAAT.Domain.Enums.SubscriptionPlan requiredPlan)
    : Exception($"Cette fonctionnalité est incluse à partir de l'offre {requiredPlan}.")
{
    public MAAT.Domain.Enums.SubscriptionPlan RequiredPlan { get; } = requiredPlan;
}
