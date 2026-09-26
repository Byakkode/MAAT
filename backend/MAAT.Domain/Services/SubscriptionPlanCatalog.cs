using MAAT.Domain.Entities;
using MAAT.Domain.Enums;

namespace MAAT.Domain.Services;

// docs/specs/abonnement.md, section 1 : quelles offres se souscrivent en libre-service.
public static class SubscriptionPlanCatalog
{
    public static bool IsPaid(SubscriptionPlan plan) => plan != SubscriptionPlan.Starter;

    // Enterprise est affichée « bientôt disponible » : ni inscription ni paiement possibles.
    public static bool IsAvailable(SubscriptionPlan plan) => plan != SubscriptionPlan.Enterprise;

    public static void EnsurePurchasable(SubscriptionPlan plan)
    {
        if (!IsPaid(plan) || !IsAvailable(plan))
        {
            throw new PlanNotAvailableException(plan);
        }
    }
}
