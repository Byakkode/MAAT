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
