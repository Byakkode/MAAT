using MAAT.Domain.Enums;

namespace MAAT.Application.DTOs;

// Status null : l'entreprise n'a pas encore choisi d'offre (écran de sélection obligatoire).
// HasBillingAccount : un client existe chez le prestataire, donc le portail est accessible.
// EffectivePlan et Entitlements (docs/specs/abonnement.md, section 8) : ce que l'offre ouvre
// réellement, calculé par PlanEntitlements. L'écran s'en sert pour masquer et proposer l'offre
// supérieure ; il ne recopie aucune règle.
public sealed record SubscriptionView(
    SubscriptionPlan? Plan,
    BillingPeriod? BillingPeriod,
    SubscriptionStatus? Status,
    bool HasBillingAccount,
    SubscriptionPlan EffectivePlan,
    EntitlementsView Entitlements);

// CanStartDiagnostic tient compte des diagnostics déjà complétés par l'entreprise.
public sealed record EntitlementsView(
    bool CanStartDiagnostic,
    bool CanViewDomainScores,
    // null : toutes les recommandations déclenchées.
    int? VisibleRecommendations,
    bool CanTrackActions,
    bool CanEditActionPlan,
    bool CanEditIndicators,
    bool CanViewBenchmark,
    bool CanOpenSupportTickets,
    bool FullReport);

public sealed record StartCheckoutRequest(SubscriptionPlan Plan, BillingPeriod BillingPeriod);

public sealed record RedirectUrlResult(string Url);

public sealed record ConfirmCheckoutRequest(string SessionId);
