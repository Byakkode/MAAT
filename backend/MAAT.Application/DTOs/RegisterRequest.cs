using MAAT.Domain.Enums;

namespace MAAT.Application.DTOs;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string CompanyName,
    string SectorCode,
    CompanySizeRange SizeRange,
    string Region,
    string? Siret,
    // docs/specs/abonnement.md, section 2 : offre choisie sur la page d'accueil avant
    // l'inscription. Absente = l'utilisateur choisira sur l'écran de sélection.
    SubscriptionPlan? Plan = null,
    BillingPeriod? BillingPeriod = null);
