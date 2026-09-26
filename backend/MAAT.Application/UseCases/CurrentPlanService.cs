using MAAT.Application.Exceptions;
using MAAT.Application.Interfaces;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Application.UseCases;

// docs/specs/abonnement.md, section 8 : droits de l'entreprise de l'utilisateur connecté,
// relus à chaque appel (un changement d'offre dans la même requête doit se voir aussitôt).
// Les règles elles-mêmes vivent dans PlanEntitlements (Domain) : ce service ne fait que
// charger l'abonnement et lever PlanRequiredException.
public class CurrentPlanService(
    ISubscriptionRepository subscriptionRepository,
    IDiagnosticRepository diagnosticRepository,
    ICurrentUserContext currentUser)
{
    public async Task<PlanEntitlements> GetAsync(CancellationToken ct)
    {
        var subscription = await subscriptionRepository.FindByCompanyIdAsync(currentUser.CompanyId, ct);
        return PlanEntitlements.For(subscription?.Plan, subscription?.Status);
    }

    // Seuls les diagnostics complétés comptent, sur tout l'historique de l'entreprise, quelle
    // que soit l'offre sous laquelle ils l'ont été.
    public async Task<bool> CanStartDiagnosticAsync(PlanEntitlements entitlements, CancellationToken ct)
    {
        if (entitlements.MaxCompletedDiagnostics is null)
        {
            return true;
        }

        var completed = await diagnosticRepository.FindAllCompletedForCurrentCompanyAsync(ct);
        return entitlements.CanStartDiagnostic(completed.Count);
    }

    // Lève si le droit demandé n'est pas ouvert par l'offre effective. requiredPlan : la
    // première offre qui l'inclut, annoncée à l'écran.
    public async Task<PlanEntitlements> EnsureAsync(
        Func<PlanEntitlements, bool> right, SubscriptionPlan requiredPlan, CancellationToken ct)
    {
        var entitlements = await GetAsync(ct);
        if (!right(entitlements))
        {
            throw new PlanRequiredException(requiredPlan);
        }

        return entitlements;
    }
}
