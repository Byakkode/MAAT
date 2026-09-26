namespace MAAT.Domain.Enums;

// État d'un abonnement chez le prestataire de paiement, ramené à ce qui compte pour MAAT.
// La traduction depuis les statuts propres à Stripe vit dans MAAT.Infrastructure : le
// Domain ne connaît pas le prestataire (docs/specs/abonnement.md, section 4).
public enum ProviderSubscriptionState
{
    Active,
    PastDue,
    // Paiement initial en cours (authentification 3-D Secure, par exemple).
    Incomplete,
    // Résilié, ou jamais payé dans le délai imparti.
    Ended,
}
