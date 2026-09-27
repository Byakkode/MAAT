using System.Globalization;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;

namespace MAAT.Domain.Services;

// État du suivi d'une action à un instant donné (ActionItemProgress, ou l'état initial quand
// aucun suivi n'existe encore).
public sealed record ActionItemState(ActionItemStatus Status, string? AssignedTo, DateTimeOffset? DueDate, string? Notes);

public sealed record ActionItemFieldChange(ActionItemField Field, string? OldValue, string? NewValue);

// docs/specs/recommandations.md, section 4 bis. Règles pures de l'historique du suivi : quels
// champs ont changé, et quand une modification de notes prolonge la ligne précédente. Sans
// I/O, testées dans MAAT.Domain.Tests.
public static class ActionItemHistory
{
    // Les notes s'enregistrent automatiquement pendant la frappe : sans regroupement, chaque
    // pause de plus d'une seconde écrirait une ligne « notes modifiées ». Dix minutes couvrent
    // une séance de rédaction sans fusionner deux séances distinctes de la même journée.
    public static readonly TimeSpan NotesMergeWindow = TimeSpan.FromMinutes(10);

    public static ActionItemState InitialState { get; } = new(ActionItemStatus.Planned, null, null, null);

    // Ordre fixe (statut, responsable, échéance, notes) : l'historique d'une modification
    // groupée se lit toujours dans le même sens.
    public static IReadOnlyList<ActionItemFieldChange> Diff(ActionItemState before, ActionItemState after)
    {
        var changes = new List<ActionItemFieldChange>();

        if (before.Status != after.Status)
        {
            changes.Add(new(ActionItemField.Status, before.Status.ToString(), after.Status.ToString()));
        }

        var (assignedBefore, assignedAfter) = (Normalize(before.AssignedTo), Normalize(after.AssignedTo));
        if (assignedBefore != assignedAfter)
        {
            changes.Add(new(ActionItemField.AssignedTo, assignedBefore, assignedAfter));
        }

        var (dueBefore, dueAfter) = (DateOnlyText(before.DueDate), DateOnlyText(after.DueDate));
        if (dueBefore != dueAfter)
        {
            changes.Add(new(ActionItemField.DueDate, dueBefore, dueAfter));
        }

        // Contenu jamais retenu : les notes sont internes à l'entreprise et peuvent contenir
        // n'importe quoi — l'historique dit seulement qu'elles ont changé.
        if (Normalize(before.Notes) != Normalize(after.Notes))
        {
            changes.Add(new(ActionItemField.Notes, null, null));
        }

        return changes;
    }

    // Vrai quand une modification de notes suit, sans rien entre les deux, une modification de
    // notes de la même personne sur la même action, il y a moins de NotesMergeWindow : on avance
    // alors la date de la ligne existante au lieu d'en créer une.
    public static bool ExtendsPreviousNotesChange(ActionItemChange? latest, ActionItemField field, Guid userId, DateTimeOffset now) =>
        field == ActionItemField.Notes
        && latest is { Field: ActionItemField.Notes }
        && latest.ChangedByUserId == userId
        && now - latest.ChangedAt <= NotesMergeWindow;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? DateOnlyText(DateTimeOffset? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
