using MAAT.Domain.Enums;

namespace MAAT.Domain.Services;

// docs/specs/abonnement.md, section 8 (ADR 0012) : ce que chaque offre ouvre. Seul endroit
// du code qui compare des offres : les services Application la consultent, le frontend
// reçoit ces droits déjà calculés par l'API et ne recopie aucune règle.
public sealed record PlanEntitlements(
    SubscriptionPlan EffectivePlan,
    // null : illimité.
    int? MaxCompletedDiagnostics,
    bool CanViewDomainScores,
    // Les N premières recommandations par priority_rank ; null : toutes. Toutes restent
    // persistées : la limite porte sur la consultation, jamais sur le calcul.
    int? VisibleRecommendations,
    // Cocher une action terminée (DiagnosticRecommendation.is_completed).
    bool CanTrackActions,
    // Plan d'actions enrichi : statut, responsable, échéance, notes.
    bool CanEditActionPlan,
    bool CanEditIndicators,
    bool CanViewBenchmark,
    bool CanOpenSupportTickets,
    // false : rapport réduit à la page de garde et aux Mentions.
    bool FullReport)
{
    private static readonly PlanEntitlements Starter = new(
        SubscriptionPlan.Starter,
        MaxCompletedDiagnostics: 1,
        CanViewDomainScores: false,
        VisibleRecommendations: 3,
        CanTrackActions: false,
        CanEditActionPlan: false,
        CanEditIndicators: false,
        CanViewBenchmark: false,
        CanOpenSupportTickets: false,
        FullReport: false);

    private static readonly PlanEntitlements Essential = new(
        SubscriptionPlan.Essential,
        MaxCompletedDiagnostics: null,
        CanViewDomainScores: true,
        VisibleRecommendations: 12,
        CanTrackActions: true,
        CanEditActionPlan: false,
        CanEditIndicators: false,
        CanViewBenchmark: false,
        CanOpenSupportTickets: true,
        FullReport: true);

    private static readonly PlanEntitlements Professional = Essential with
    {
        EffectivePlan = SubscriptionPlan.Professional,
        VisibleRecommendations = null,
        CanEditActionPlan = true,
        CanEditIndicators = true,
        CanViewBenchmark = true,
    };

    // Paramètres nullables : une entreprise qui n'a encore rien choisi n'a pas de ligne
    // Subscription.
    public static PlanEntitlements For(SubscriptionPlan? plan, SubscriptionStatus? status) =>
        EffectivePlanOf(plan, status) switch
        {
            SubscriptionPlan.Essential => Essential,
            SubscriptionPlan.Professional => Professional,
            // Pas encore commercialisée : les droits de Professional, en attendant les siens.
            SubscriptionPlan.Enterprise => Professional with { EffectivePlan = SubscriptionPlan.Enterprise },
            _ => Starter,
        };

    // Paiement en attente : rien n'est encore payé, donc droits du Starter. Impayé
    // (PastDue) : les droits payants restent pendant les relances du prestataire ; s'il
    // abandonne, le webhook de fin d'abonnement ramène l'entreprise sur Starter.
    private static SubscriptionPlan EffectivePlanOf(SubscriptionPlan? plan, SubscriptionStatus? status) =>
        status is SubscriptionStatus.Active or SubscriptionStatus.PastDue && plan is { } chosen
            ? chosen
            : SubscriptionPlan.Starter;

    public bool IsRecommendationVisible(int priorityRank) =>
        VisibleRecommendations is not { } max || priorityRank <= max;

    // Les recommandations visibles d'une liste déjà triée par priority_rank.
    public IReadOnlyList<T> TakeVisibleRecommendations<T>(IReadOnlyList<T> sortedByPriority) =>
        VisibleRecommendations is { } max ? [.. sortedByPriority.Take(max)] : sortedByPriority;

    // Seuls les diagnostics complétés comptent : un diagnostic abandonné ne consomme pas
    // l'évaluation du Starter.
    public bool CanStartDiagnostic(int completedDiagnostics) =>
        MaxCompletedDiagnostics is not { } max || completedDiagnostics < max;
}
