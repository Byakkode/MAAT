using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Domain.Tests;

// docs/specs/recommandations.md, section 4 bis : ce que l'historique du suivi retient d'une
// modification, et quand une modification de notes prolonge la précédente au lieu d'en créer
// une nouvelle.
public class ActionItemHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid Diagnostic = Guid.NewGuid();
    private static readonly Guid Claire = Guid.NewGuid();
    private static readonly Guid Paul = Guid.NewGuid();

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

    // Les notes s'enregistrent automatiquement pendant la frappe (PlanActionsPage) : une
    // rédaction continue ne doit donner qu'une ligne.
    [Theory]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void Notes_de_la_meme_personne_dans_les_10_minutes_prolongent_la_ligne_precedente(int minutesLater, bool expected)
    {
        var previous = new ActionItemChange(Diagnostic, "ENV-01", new ActionItemFieldChange(ActionItemField.Notes, null, null), Claire, Now);

        Assert.Equal(expected, ActionItemHistory.ExtendsPreviousNotesChange(previous, ActionItemField.Notes, Claire, Now.AddMinutes(minutesLater)));
    }

    [Fact]
    public void Notes_d_une_autre_personne_donnent_une_nouvelle_ligne()
    {
        var previous = new ActionItemChange(Diagnostic, "ENV-01", new ActionItemFieldChange(ActionItemField.Notes, null, null), Claire, Now);

        Assert.False(ActionItemHistory.ExtendsPreviousNotesChange(previous, ActionItemField.Notes, Paul, Now.AddMinutes(1)));
    }

    // Seule une suite ininterrompue de notes se regroupe : un changement de statut entre deux
    // séances de rédaction les sépare, et les autres champs ne se regroupent jamais.
    [Fact]
    public void Seules_les_notes_se_regroupent_et_seulement_derriere_des_notes()
    {
        var statusChange = new ActionItemChange(Diagnostic, "ENV-01", new ActionItemFieldChange(ActionItemField.Status, "Planned", "Done"), Claire, Now);
        var notesChange = new ActionItemChange(Diagnostic, "ENV-01", new ActionItemFieldChange(ActionItemField.Notes, null, null), Claire, Now);

        Assert.False(ActionItemHistory.ExtendsPreviousNotesChange(statusChange, ActionItemField.Notes, Claire, Now.AddMinutes(1)));
        Assert.False(ActionItemHistory.ExtendsPreviousNotesChange(notesChange, ActionItemField.Status, Claire, Now.AddMinutes(1)));
        Assert.False(ActionItemHistory.ExtendsPreviousNotesChange(null, ActionItemField.Notes, Claire, Now));
    }

    [Fact]
    public void Prolonger_une_ligne_avance_sa_date()
    {
        var change = new ActionItemChange(Diagnostic, "ENV-01", new ActionItemFieldChange(ActionItemField.Notes, null, null), Claire, Now);

        change.ExtendTo(Now.AddMinutes(5));

        Assert.Equal(Now.AddMinutes(5), change.ChangedAt);
    }
}
