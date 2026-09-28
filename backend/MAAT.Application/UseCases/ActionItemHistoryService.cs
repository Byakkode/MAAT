using MAAT.Application.DTOs;
using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Application.UseCases;

// docs/specs/recommandations.md, section 4 bis : historique du suivi d'une action. La règle
// (quels champs ont changé) vit dans ActionItemHistory (Domain) ; ce service ne fait que
// l'appliquer et persister.
public class ActionItemHistoryService(
    IActionItemChangeRepository changeRepository,
    IDiagnosticRepository diagnosticRepository,
    CurrentPlanService currentPlan,
    ICurrentUserContext currentUser,
    TimeProvider timeProvider)
{
    // Ajoute au contexte les lignes correspondant au passage de before à after, sans les
    // enregistrer : l'appelant les enregistre avec le suivi lui-même, dans le même
    // SaveChanges — un suivi modifié sans sa trace, ou l'inverse, n'existe jamais.
    // Aucun contrôle d'offre ici : seule la modification du suivi enrichi (Professional)
    // appelle cette méthode.
    // Une ligne par champ modifié, toutes à la même date.
    public void Record(Guid diagnosticId, string code, ActionItemState before, ActionItemState after)
    {
        var now = timeProvider.GetUtcNow();

        foreach (var change in ActionItemHistory.Diff(before, after))
        {
            changeRepository.Add(new ActionItemChange(diagnosticId, code, change, currentUser.UserId, now));
        }
    }

    // null : diagnostic inconnu ou d'une autre entreprise (404). Lecture réservée à
    // Professional (abonnement.md, section 8), tous rôles confondus.
    public async Task<IReadOnlyList<ActionItemChangeView>?> GetAsync(Guid diagnosticId, string code, CancellationToken ct)
    {
        await currentPlan.EnsureAsync(e => e.CanViewActionHistory, SubscriptionPlan.Professional, ct);

        if (await diagnosticRepository.FindByIdAsync(diagnosticId, ct) is null)
        {
            return null;
        }

        return await changeRepository.ListAsync(diagnosticId, code, ct);
    }
}
