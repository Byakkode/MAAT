using MAAT.Application.Interfaces;

namespace MAAT.Application.UseCases;

// docs/specs/abonnement.md, section 5 : webhook du prestataire de paiement. Filet de sécurité
// qui voit tout ce que le navigateur ne voit pas — renouvellements, impayés, résiliation en
// fin de période, onglet fermé avant le retour de Stripe. Séparé de BillingService parce qu'il
// s'exécute sans utilisateur authentifié : l'entreprise vient des métadonnées que MAAT a posées
// sur l'abonnement à sa création.
public class BillingWebhookHandler(IPaymentGateway paymentGateway, SubscriptionSynchronizer synchronizer)
{
    public async Task HandleAsync(string payload, string signatureHeader, CancellationToken ct)
    {
        var snapshot = await paymentGateway.ReadWebhookAsync(payload, signatureHeader, ct);
        if (snapshot is not null)
        {
            await synchronizer.ApplyAsync(snapshot, ct);
        }
    }
}
