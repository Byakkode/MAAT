namespace MAAT.Domain.Enums;

// docs/specs/abonnement.md, section 1. Enterprise existe dans la grille tarifaire mais
// n'est pas encore commercialisée (SubscriptionPlanCatalog.IsAvailable).
public enum SubscriptionPlan
{
    Starter,
    Essential,
    Professional,
    Enterprise,
}
