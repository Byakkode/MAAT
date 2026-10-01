using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Domain.Tests;

// docs/specs/abonnement.md, section 8 : offre effective selon le statut de l'abonnement,
// puis droits de chaque offre, ligne par ligne du tableau de la spec.
public class PlanEntitlementsTests
{
    [Fact]
    public void OffreEffective_SansAbonnement_Starter()
    {
        Assert.Equal(SubscriptionPlan.Starter, PlanEntitlements.For(null, null).EffectivePlan);
    }

    [Fact]
    public void OffreEffective_PaiementEnAttente_Starter()
    {
        var entitlements = PlanEntitlements.For(SubscriptionPlan.Professional, SubscriptionStatus.PendingPayment);

        Assert.Equal(SubscriptionPlan.Starter, entitlements.EffectivePlan);
    }

    [Fact]
    public void OffreEffective_Active_OffreSouscrite()
    {
        var entitlements = PlanEntitlements.For(SubscriptionPlan.Essential, SubscriptionStatus.Active);

        Assert.Equal(SubscriptionPlan.Essential, entitlements.EffectivePlan);
    }

    [Fact]
    public void OffreEffective_Impaye_GardeLOffreSouscrite()
    {
        var entitlements = PlanEntitlements.For(SubscriptionPlan.Professional, SubscriptionStatus.PastDue);

        Assert.Equal(SubscriptionPlan.Professional, entitlements.EffectivePlan);
    }

    [Fact]
    public void Starter_DroitsDuTableau()
    {
        var starter = PlanEntitlements.For(SubscriptionPlan.Starter, SubscriptionStatus.Active);

        Assert.Equal(1, starter.MaxCompletedDiagnostics);
        Assert.False(starter.CanViewDomainScores);
        Assert.Equal(3, starter.VisibleRecommendations);
        Assert.False(starter.CanTrackActions);
        Assert.False(starter.CanEditActionPlan);
        Assert.False(starter.CanEditIndicators);
        Assert.False(starter.CanViewBenchmark);
        Assert.False(starter.CanOpenSupportTickets);
        Assert.False(starter.FullReport);
        Assert.False(starter.CanCustomizeReportLogo);
        Assert.False(starter.CanViewActionHistory);
        Assert.False(starter.CanReadDocumentation);
    }

    [Fact]
    public void Essential_DroitsDuTableau()
    {
        var essential = PlanEntitlements.For(SubscriptionPlan.Essential, SubscriptionStatus.Active);

        Assert.Null(essential.MaxCompletedDiagnostics);
        Assert.True(essential.CanViewDomainScores);
        Assert.Equal(12, essential.VisibleRecommendations);
        Assert.True(essential.CanTrackActions);
        Assert.False(essential.CanEditActionPlan);
        // norme-volontaire.md : le rapport selon la norme volontaire (Essential) en dépend.
        Assert.True(essential.CanEditIndicators);
        Assert.False(essential.CanViewBenchmark);
        Assert.True(essential.CanOpenSupportTickets);
        Assert.True(essential.FullReport);
        Assert.True(essential.CanCustomizeReportLogo);
        Assert.False(essential.CanViewActionHistory);
        Assert.True(essential.CanReadDocumentation);
    }

    [Fact]
    public void Professional_DroitsDuTableau()
    {
        var professional = PlanEntitlements.For(SubscriptionPlan.Professional, SubscriptionStatus.Active);

        Assert.Null(professional.MaxCompletedDiagnostics);
        Assert.True(professional.CanViewDomainScores);
        Assert.Null(professional.VisibleRecommendations);
        Assert.True(professional.CanTrackActions);
        Assert.True(professional.CanEditActionPlan);
        Assert.True(professional.CanEditIndicators);
        Assert.True(professional.CanViewBenchmark);
        Assert.True(professional.CanOpenSupportTickets);
        Assert.True(professional.FullReport);
        Assert.True(professional.CanCustomizeReportLogo);
        Assert.True(professional.CanViewActionHistory);
        Assert.True(professional.CanReadDocumentation);
    }

    [Fact]
    public void Enterprise_AligneeSurProfessional()
    {
        var enterprise = PlanEntitlements.For(SubscriptionPlan.Enterprise, SubscriptionStatus.Active);
        var professional = PlanEntitlements.For(SubscriptionPlan.Professional, SubscriptionStatus.Active);

        Assert.Equal(professional with { EffectivePlan = SubscriptionPlan.Enterprise }, enterprise);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(4, false)]
    public void Starter_UnSeulDiagnosticComplete(int completedDiagnostics, bool expected)
    {
        var starter = PlanEntitlements.For(SubscriptionPlan.Starter, SubscriptionStatus.Active);

        Assert.Equal(expected, starter.CanStartDiagnostic(completedDiagnostics));
    }

    [Fact]
    public void Essential_DiagnosticsIllimites()
    {
        var essential = PlanEntitlements.For(SubscriptionPlan.Essential, SubscriptionStatus.Active);

        Assert.True(essential.CanStartDiagnostic(50));
    }

    // Trois paliers : 3, 12, toutes (null).
    [Theory]
    [InlineData(SubscriptionPlan.Starter, 3, true)]
    [InlineData(SubscriptionPlan.Starter, 4, false)]
    [InlineData(SubscriptionPlan.Essential, 12, true)]
    [InlineData(SubscriptionPlan.Essential, 13, false)]
    [InlineData(SubscriptionPlan.Professional, 45, true)]
    public void RecommandationVisibleSelonSonRang(SubscriptionPlan plan, int priorityRank, bool expected)
    {
        var entitlements = PlanEntitlements.For(plan, SubscriptionStatus.Active);

        Assert.Equal(expected, entitlements.IsRecommendationVisible(priorityRank));
    }
}
