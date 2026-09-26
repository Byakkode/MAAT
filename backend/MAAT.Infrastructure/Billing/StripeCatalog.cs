using MAAT.Domain.Enums;

namespace MAAT.Infrastructure.Billing;

// docs/specs/abonnement.md, section 7 : correspondance entre les offres MAAT et le catalogue
// Stripe. Les prix sont retrouvés par leur clé de recherche (lookup key), saisie une fois
// dans le tableau de bord Stripe : aucun identifiant price_… n'est à recopier dans la
// configuration, et les mêmes clés servent en mode test comme en production.
public static class StripeCatalog
{
    // Métadonnée posée sur chaque abonnement Stripe créé par MAAT : c'est elle qui relie un
    // webhook à une entreprise, sans que Stripe connaisse autre chose que cet identifiant.
    public const string CompanyIdMetadataKey = "maat_company_id";

    public static string LookupKey(SubscriptionPlan plan, BillingPeriod period) =>
        $"{plan.ToString().ToLowerInvariant()}_{(period == BillingPeriod.Monthly ? "monthly" : "yearly")}";

    public static bool TryParseLookupKey(string? lookupKey, out SubscriptionPlan plan, out BillingPeriod period)
    {
        plan = default;
        period = default;

        var parts = lookupKey?.Split('_');
        if (parts is not { Length: 2 }
            || !Enum.TryParse(parts[0], ignoreCase: true, out plan)
            || plan == SubscriptionPlan.Starter)
        {
            return false;
        }

        switch (parts[1])
        {
            case "monthly":
                period = BillingPeriod.Monthly;
                return true;
            case "yearly":
                period = BillingPeriod.Yearly;
                return true;
            default:
                return false;
        }
    }

    // Statuts d'abonnement Stripe (https://docs.stripe.com/api/subscriptions/object#subscription_object-status).
    // null : statut inconnu d'une version future de l'API, laissé sans effet plutôt que deviné.
    public static ProviderSubscriptionState? MapStatus(string? status) => status switch
    {
        "active" or "trialing" => ProviderSubscriptionState.Active,
        "past_due" or "unpaid" or "paused" => ProviderSubscriptionState.PastDue,
        "incomplete" => ProviderSubscriptionState.Incomplete,
        "incomplete_expired" or "canceled" => ProviderSubscriptionState.Ended,
        _ => null,
    };
}
