using System.Globalization;
using MAAT.Domain.Enums;

namespace MAAT.Domain.Services;

// État du suivi d'une action à un instant donné (ActionItemProgress, ou l'état initial quand
// aucun suivi n'existe encore).
public sealed record ActionItemState(ActionItemStatus Status, string? AssignedTo, DateTimeOffset? DueDate, string? Notes);

public sealed record ActionItemFieldChange(ActionItemField Field, string? OldValue, string? NewValue);

// docs/specs/recommandations.md, section 4 bis. Règle pure de l'historique du suivi : quels
// champs ont changé entre deux états. Sans I/O, testée dans MAAT.Domain.Tests.
// Pas de regroupement : l'écran n'enregistre que sur une action volontaire (menu de statut,
// bouton « Enregistrer »), jamais à la frappe — chaque enregistrement mérite sa ligne.
public static class ActionItemHistory
{
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

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? DateOnlyText(DateTimeOffset? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
