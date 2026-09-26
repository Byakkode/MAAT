using MAAT.Domain.Enums;

namespace MAAT.Application.DTOs;

// Status null : l'entreprise n'a pas encore choisi d'offre (écran de sélection obligatoire).
// HasBillingAccount : un client existe chez le prestataire, donc le portail est accessible.
public sealed record SubscriptionView(
    SubscriptionPlan? Plan,
    BillingPeriod? BillingPeriod,
    SubscriptionStatus? Status,
    bool HasBillingAccount);

public sealed record StartCheckoutRequest(SubscriptionPlan Plan, BillingPeriod BillingPeriod);

public sealed record RedirectUrlResult(string Url);

public sealed record ConfirmCheckoutRequest(string SessionId);
