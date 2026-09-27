using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/abonnement.md, section 8 : place une entreprise sur une offre donnée sans passer
// par le prestataire de paiement. Une offre payante est posée comme si Stripe l'avait
// confirmée (Subscription.FromProvider / ApplyProviderState), seul chemin par lequel une offre
// payante devient active en production.
internal static class TestSubscriptions
{
    public static async Task SetPlanAsync(
        MaatDbContext context,
        Guid companyId,
        SubscriptionPlan plan,
        ProviderSubscriptionState state = ProviderSubscriptionState.Active)
    {
        var existing = await context.Subscriptions.SingleOrDefaultAsync(s => s.CompanyId == companyId);

        if (plan == SubscriptionPlan.Starter)
        {
            if (existing is null)
            {
                context.Subscriptions.Add(Subscription.StartStarter(companyId));
            }
            else
            {
                // Retour à Starter tel que le produit le vit : fin de l'abonnement chez le
                // prestataire, jamais une bascule directe (refusée tant qu'il est en cours).
                existing.ApplyProviderState(
                    existing.StripeCustomerId ?? "cus_test", existing.StripeSubscriptionId ?? "sub_test",
                    existing.Plan, existing.BillingPeriod ?? BillingPeriod.Monthly, ProviderSubscriptionState.Ended);
                if (existing.Plan != SubscriptionPlan.Starter)
                {
                    existing.SwitchToStarter();
                }
            }
        }
        else if (existing is null)
        {
            context.Subscriptions.Add(Subscription.FromProvider(
                companyId, $"cus_test_{companyId:N}", $"sub_test_{companyId:N}", plan, BillingPeriod.Monthly, state));
        }
        else
        {
            existing.ApplyProviderState(
                existing.StripeCustomerId ?? $"cus_test_{companyId:N}", existing.StripeSubscriptionId ?? $"sub_test_{companyId:N}",
                plan, BillingPeriod.Monthly, state);
        }

        await context.SaveChangesAsync();
    }
}
