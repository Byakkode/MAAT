namespace MAAT.Domain.Enums;

// docs/specs/abonnement.md, section 3. Pas de statut « résilié » : un abonnement payant
// qui se termine ramène l'entreprise sur Starter, qui reste actif à vie.
public enum SubscriptionStatus
{
    // Offre payante choisie à l'inscription, paiement pas encore reçu.
    PendingPayment,
    Active,
    // Échéance impayée, le prestataire relance le paiement.
    PastDue,
}
