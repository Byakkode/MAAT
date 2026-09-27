using MAAT.Application.DTOs;
using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Application.UseCases;

// docs/specs/recommandations.md, section 4 bis : historique du suivi d'une action. Les règles
// (champs modifiés, regroupement des notes) vivent dans ActionItemHistory (Domain) ; ce
// service ne fait que les appliquer et persister.
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
    public async Task RecordAsync(Guid diagnosticId, string code, ActionItemState before, ActionItemState after, CancellationToken ct)
    {
        var changes = ActionItemHistory.Diff(before, after);
        if (changes.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var latest = await changeRepository.FindLatestAsync(diagnosticId, code, ct);

        foreach (var change in changes)
        {
            if (ActionItemHistory.ExtendsPreviousNotesChange(latest, change.Field, currentUser.UserId, now))
            {
                latest!.ExtendTo(now);
                continue;
            }

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
