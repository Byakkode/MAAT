// docs/specs/coquille-et-compte.md, section 7 : "Un lien d'évitement en tête de page, visible
// au focus, menant au contenu principal." Premier élément focalisable de l'arbre — placé avant
// la sidebar dans le DOM pour être le premier arrêt au Tab — invisible tant qu'il n'a pas le
// focus (sr-only, révélé par focus:not-sr-only).
export function SkipLink() {
  return (
    <a
      href="#contenu-principal"
      className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded-button focus:bg-white focus:px-4 focus:py-2 focus:font-medium focus:text-blue-maat-text focus:shadow-card"
    >
      Aller au contenu principal
    </a>
  )
}
