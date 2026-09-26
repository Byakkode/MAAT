using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;

namespace MAAT.Application.UseCases;

// docs/specs/abonnement.md, section 5 : applique à l'abonnement MAAT un état relu chez le
// prestataire de paiement. Trois chemins y mènent — webhook, retour de la page de paiement,
// retour du portail client — et tous passent ici : l'état appliqué vient toujours de Stripe,
// jamais du navigateur, et l'appliquer deux fois ne change rien.
//
// Ne dépend pas d'ICurrentUserContext (docs/adr/0005) : le webhook n'a pas d'utilisateur
// authentifié. Les appelants liés à une requête utilisateur vérifient eux-mêmes que l'état
// concerne bien l'entreprise du jeton avant de l'appliquer.
public class SubscriptionSynchronizer(
    ISubscriptionRepository subscriptionRepository,
    ICompanyRepository companyRepository,
    IUnitOfWork unitOfWork)
{
    public async Task ApplyAsync(ProviderSubscriptionSnapshot snapshot, CancellationToken ct)
    {
        // Entreprise supprimée entre-temps (droit à l'effacement) : rien à mettre à jour.
        if (await companyRepository.GetByIdAsync(snapshot.CompanyId, ct) is null)
        {
            return;
        }

        var subscription = await subscriptionRepository.FindByCompanyIdAsync(snapshot.CompanyId, ct);
        if (subscription is null)
        {
            if (snapshot.State == ProviderSubscriptionState.Ended)
            {
                return;
            }

            await subscriptionRepository.AddAsync(
                Subscription.FromProvider(snapshot.CompanyId, snapshot.CustomerId, snapshot.SubscriptionId, snapshot.Plan, snapshot.Period, snapshot.State),
                ct);
        }
        else
        {
            subscription.ApplyProviderState(snapshot.CustomerId, snapshot.SubscriptionId, snapshot.Plan, snapshot.Period, snapshot.State);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}
