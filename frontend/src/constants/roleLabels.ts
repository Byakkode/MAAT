// docs/specs/coquille-et-compte.md, section 4 : "Le rôle est affiché en toutes lettres [...]
// un utilisateur qui se voit refuser une action doit pouvoir comprendre pourquoi." Les valeurs
// brutes (Admin/User/Viewer) sont les noms de MAAT.Domain.Enums.UserRole, jamais montrées
// telles quelles à l'écran — utilisé par le menu de l'en-tête et le bloc Identité du compte.
export const ROLE_LABELS: Record<string, string> = {
  Admin: 'Administrateur',
  User: 'Utilisateur',
  Viewer: 'Lecteur',
}
