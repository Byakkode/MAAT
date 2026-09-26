using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Domain.Tests;

// docs/specs/abonnement.md, section 3 : règles de l'abonnement d'une entreprise. Le
// prestataire de paiement (Stripe) n'apparaît pas ici : l'entité ne connaît que l'état
// normalisé qu'il transmet (ProviderSubscriptionState).
public class SubscriptionTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Fact]
    public void ChoixAInscription_Starter_ActifSansPeriode()
    {
        var subscription = Subscription.ChooseAtRegistration(CompanyId, SubscriptionPlan.Starter, BillingPeriod.Yearly);

        Assert.Equal(SubscriptionPlan.Starter, subscription.Plan);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Null(subscription.BillingPeriod);
    }

    [Fact]
    public void ChoixAInscription_OffrePayante_EnAttenteDePaiement()
    {
        var subscription = Subscription.ChooseAtRegistration(CompanyId, SubscriptionPlan.Essential, BillingPeriod.Yearly);

        Assert.Equal(SubscriptionPlan.Essential, subscription.Plan);
        Assert.Equal(BillingPeriod.Yearly, subscription.BillingPeriod);
        Assert.Equal(SubscriptionStatus.PendingPayment, subscription.Status);
    }

    [Fact]
    public void ChoixAInscription_OffrePayanteSansPeriode_MensuelleParDefaut()
    {
        var subscription = Subscription.ChooseAtRegistration(CompanyId, SubscriptionPlan.Professional, null);

        Assert.Equal(BillingPeriod.Monthly, subscription.BillingPeriod);
    }

    [Fact]
    public void ChoixAInscription_Enterprise_Refuse()
    {
        Assert.Throws<PlanNotAvailableException>(() =>
            Subscription.ChooseAtRegistration(CompanyId, SubscriptionPlan.Enterprise, BillingPeriod.Monthly));
    }

    [Theory]
    [InlineData(SubscriptionPlan.Starter)]
    [InlineData(SubscriptionPlan.Enterprise)]
    public void Paiement_OffreNonAchetable_Refuse(SubscriptionPlan plan)
    {
        Assert.Throws<PlanNotAvailableException>(() => SubscriptionPlanCatalog.EnsurePurchasable(plan));
    }

    [Fact]
    public void EtatPrestataire_Actif_ActiveLOffreEtConserveLesIdentifiants()
    {
        var subscription = Subscription.ChooseAtRegistration(CompanyId, SubscriptionPlan.Essential, BillingPeriod.Monthly);

        subscription.ApplyProviderState("cus_1", "sub_1", SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Active);

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal("cus_1", subscription.StripeCustomerId);
        Assert.Equal("sub_1", subscription.StripeSubscriptionId);
        Assert.True(subscription.HasPaidProviderSubscription);
    }

    [Fact]
    public void EtatPrestataire_ChangementDOffreDepuisLePortail_RepriseTelle()
    {
        var subscription = Subscription.FromProvider(CompanyId, "cus_1", "sub_1", SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Active);

        subscription.ApplyProviderState("cus_1", "sub_1", SubscriptionPlan.Professional, BillingPeriod.Yearly, ProviderSubscriptionState.Active);

        Assert.Equal(SubscriptionPlan.Professional, subscription.Plan);
        Assert.Equal(BillingPeriod.Yearly, subscription.BillingPeriod);
    }

    [Fact]
    public void EtatPrestataire_ImpayeEnCours_PastDue()
    {
        var subscription = Subscription.FromProvider(CompanyId, "cus_1", "sub_1", SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Active);

        subscription.ApplyProviderState("cus_1", "sub_1", SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.PastDue);

        Assert.Equal(SubscriptionStatus.PastDue, subscription.Status);
    }

    [Fact]
    public void EtatPrestataire_AbonnementTermine_RetourAStarterEnGardantLeClient()
    {
        var subscription = Subscription.FromProvider(CompanyId, "cus_1", "sub_1", SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Active);

        subscription.ApplyProviderState("cus_1", "sub_1", SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Ended);

        Assert.Equal(SubscriptionPlan.Starter, subscription.Plan);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Null(subscription.BillingPeriod);
        Assert.Null(subscription.StripeSubscriptionId);
        Assert.Equal("cus_1", subscription.StripeCustomerId);
        Assert.False(subscription.HasPaidProviderSubscription);
    }

    // Les webhooks n'arrivent pas forcément dans l'ordre : la fin d'un ancien abonnement,
    // reçue après la souscription d'un nouveau, ne doit pas faire retomber l'entreprise
    // sur Starter.
    [Fact]
    public void EtatPrestataire_FinDUnAncienAbonnement_Ignoree()
    {
        var subscription = Subscription.FromProvider(CompanyId, "cus_1", "sub_new", SubscriptionPlan.Professional, BillingPeriod.Monthly, ProviderSubscriptionState.Active);

        subscription.ApplyProviderState("cus_1", "sub_old", SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Ended);

        Assert.Equal(SubscriptionPlan.Professional, subscription.Plan);
        Assert.Equal("sub_new", subscription.StripeSubscriptionId);
    }

    [Fact]
    public void RetourAStarter_SansAbonnementPayant_Accepte()
    {
        var subscription = Subscription.ChooseAtRegistration(CompanyId, SubscriptionPlan.Essential, BillingPeriod.Monthly);

        subscription.SwitchToStarter();

        Assert.Equal(SubscriptionPlan.Starter, subscription.Plan);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
    }

    // Un abonnement payant se résilie chez le prestataire (portail client) : le repasser
    // à Starter ici laisserait la facturation continuer sans contrepartie visible.
    [Fact]
    public void RetourAStarter_AvecAbonnementPayantActif_Refuse()
    {
        var subscription = Subscription.FromProvider(CompanyId, "cus_1", "sub_1", SubscriptionPlan.Essential, BillingPeriod.Monthly, ProviderSubscriptionState.Active);

        Assert.Throws<PaidSubscriptionActiveException>(subscription.SwitchToStarter);
    }
}
