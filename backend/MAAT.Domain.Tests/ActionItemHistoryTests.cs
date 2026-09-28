using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Domain.Tests;

// docs/specs/recommandations.md, section 4 bis : ce que l'historique du suivi retient d'une
// modification.
public class ActionItemHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 14, 0, 0, TimeSpan.Zero);

    private static ActionItemState State(
        ActionItemStatus status = ActionItemStatus.Planned, string? assignedTo = null, DateTimeOffset? dueDate = null, string? notes = null) =>
        new(status, assignedTo, dueDate, notes);

    [Fact]
    public void Aucun_changement_aucune_ligne()
    {
        var state = State(ActionItemStatus.InProgress, "Claire", Now, "texte");

        Assert.Empty(ActionItemHistory.Diff(state, state));
    }

    [Fact]
    public void Chaque_champ_modifie_donne_sa_ligne_avec_ancienne_et_nouvelle_valeur()
    {
        var changes = ActionItemHistory.Diff(
            ActionItemHistory.InitialState,
            State(ActionItemStatus.InProgress, "Claire Martin", new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(
            [
                new ActionItemFieldChange(ActionItemField.Status, "Planned", "InProgress"),
                new ActionItemFieldChange(ActionItemField.AssignedTo, null, "Claire Martin"),
                new ActionItemFieldChange(ActionItemField.DueDate, null, "2026-11-15"),
            ],
            changes);
    }

    // Choix validé avec l'utilisateur : « notes modifiées », jamais leur contenu.
    [Fact]
    public void Notes_modifiees_sans_leur_texte()
    {
        var change = Assert.Single(ActionItemHistory.Diff(State(notes: "ancien"), State(notes: "nouveau secret")));

        Assert.Equal(new ActionItemFieldChange(ActionItemField.Notes, null, null), change);
    }

    // Un champ vidé à l'écran arrive en "" ou en null selon le chemin : même valeur.
    [Fact]
    public void Chaine_vide_et_null_sont_equivalentes()
    {
        Assert.Empty(ActionItemHistory.Diff(State(assignedTo: null, notes: null), State(assignedTo: "", notes: "  ")));
    }

    // L'échéance est une date : un décalage d'heure ou de fuseau ne la change pas.
    [Fact]
    public void Echeance_comparee_au_jour()
    {
        var morning = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
        var evening = new DateTimeOffset(2026, 11, 15, 18, 30, 0, TimeSpan.Zero);

        Assert.Empty(ActionItemHistory.Diff(State(dueDate: morning), State(dueDate: evening)));
    }

    [Fact]
    public void Echeance_retiree()
    {
        var change = Assert.Single(ActionItemHistory.Diff(State(dueDate: Now), State(dueDate: null)));

        Assert.Equal(new ActionItemFieldChange(ActionItemField.DueDate, "2026-10-12", null), change);
    }
}
