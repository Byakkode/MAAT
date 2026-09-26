using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

// Pas de filtre implicite par ICurrentUserContext (docs/adr/0005) : le webhook du
// prestataire de paiement n'a pas d'utilisateur authentifié, il identifie l'entreprise par
// les métadonnées de l'abonnement. Les appels depuis une requête utilisateur passent
// toujours currentUser.CompanyId (BillingService), jamais une valeur reçue du client.
public interface ISubscriptionRepository
{
    Task<Subscription?> FindByCompanyIdAsync(Guid companyId, CancellationToken ct);

    Task AddAsync(Subscription subscription, CancellationToken ct);
}
