using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Domain.Entities;

// docs/specs/recommandations.md, section 4 bis : une ligne de l'historique du suivi d'une
// action. Jamais modifiée après coup.
// OldValue/NewValue : valeurs brutes (nom de statut, texte du responsable, date aaaa-mm-jj),
// mises en forme par l'écran. Toujours null pour les notes, dont le contenu n'est pas retenu.
// ChangedByUserId : null une fois le compte supprimé — la ligne reste, anonyme.
public class ActionItemChange
{
    public Guid Id { get; private set; }
    public Guid DiagnosticId { get; private set; }
    public string RecommendationCode { get; private set; } = default!;
    public ActionItemField Field { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public Guid? ChangedByUserId { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }

    private ActionItemChange()
    {
    }

    public ActionItemChange(Guid diagnosticId, string recommendationCode, ActionItemFieldChange change, Guid changedByUserId, DateTimeOffset changedAt)
    {
        Id = Guid.NewGuid();
        DiagnosticId = diagnosticId;
        RecommendationCode = recommendationCode;
        Field = change.Field;
        OldValue = change.OldValue;
        NewValue = change.NewValue;
        ChangedByUserId = changedByUserId;
        ChangedAt = changedAt;
    }
}
