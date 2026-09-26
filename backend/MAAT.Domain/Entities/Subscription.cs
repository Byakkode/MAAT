using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Domain.Entities;

public sealed class PlanNotAvailableException(SubscriptionPlan plan)
    : Exception($"L'offre {plan} ne peut pas être souscrite en ligne.");

public sealed class PaidSubscriptionActiveException()
    : Exception("Un abonnement payant est en cours : changez d'offre ou résiliez depuis la gestion de l'abonnement.");

// docs/specs/abonnement.md, section 3 : une ligne par entreprise, créée quand elle choisit
// une offre (à l'inscription ou sur l'écran de sélection), ou au premier paiement reçu.
// Aucune ligne = l'entreprise n'a pas encore choisi.
public class Subscription
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public SubscriptionPlan Plan { get; private set; }
    public BillingPeriod? BillingPeriod { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public string? StripeCustomerId { get; private set; }
    public string? StripeSubscriptionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool HasPaidProviderSubscription => StripeSubscriptionId is not null;

    private Subscription()
    {
    }

    private Subscription(Guid companyId)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // Offre transmise par la page d'accueil (« Choisir Essential » → inscription) : Starter
    // est actif tout de suite, une offre payante attend le paiement de la première connexion.
    public static Subscription ChooseAtRegistration(Guid companyId, SubscriptionPlan plan, BillingPeriod? period)
    {
        var subscription = new Subscription(companyId);
        if (plan == SubscriptionPlan.Starter)
        {
            subscription.SetStarter();
            return subscription;
        }

        SubscriptionPlanCatalog.EnsurePurchasable(plan);
        subscription.Plan = plan;
        subscription.BillingPeriod = period ?? Enums.BillingPeriod.Monthly;
        subscription.Status = SubscriptionStatus.PendingPayment;
        subscription.UpdatedAt = DateTimeOffset.UtcNow;
        return subscription;
    }

    public static Subscription StartStarter(Guid companyId)
    {
        var subscription = new Subscription(companyId);
        subscription.SetStarter();
        return subscription;
    }

    // Premier paiement reçu pour une entreprise qui n'avait encore rien choisi.
    public static Subscription FromProvider(
        Guid companyId, string customerId, string subscriptionId,
        SubscriptionPlan plan, BillingPeriod period, ProviderSubscriptionState state)
    {
        var subscription = new Subscription(companyId);
        subscription.ApplyProviderState(customerId, subscriptionId, plan, period, state);
        return subscription;
    }

    public void SwitchToStarter()
    {
        if (HasPaidProviderSubscription)
        {
            throw new PaidSubscriptionActiveException();
        }

        SetStarter();
    }

    // Le prestataire fait foi : chaque webhook transmet l'état courant de l'abonnement, relu
    // chez lui au moment du traitement, jamais un delta à rejouer.
    public void ApplyProviderState(
        string customerId, string subscriptionId,
        SubscriptionPlan plan, BillingPeriod period, ProviderSubscriptionState state)
    {
        if (state == ProviderSubscriptionState.Ended)
        {
            // Fin d'un abonnement qui n'est plus le courant (webhook arrivé en retard) : rien
            // à défaire.
            if (StripeSubscriptionId != subscriptionId)
            {
                return;
            }

            StripeSubscriptionId = null;
            SetStarter();
            return;
        }

        StripeCustomerId = customerId;
        StripeSubscriptionId = subscriptionId;
        Plan = plan;
        BillingPeriod = period;
        Status = state switch
        {
            ProviderSubscriptionState.Active => SubscriptionStatus.Active,
            ProviderSubscriptionState.PastDue => SubscriptionStatus.PastDue,
            _ => SubscriptionStatus.PendingPayment,
        };
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void SetStarter()
    {
        Plan = SubscriptionPlan.Starter;
        BillingPeriod = null;
        Status = SubscriptionStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
