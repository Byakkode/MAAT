interface SaveStatusBannerProps {
  hasError: boolean
  onRetry: () => void
}

// docs/specs/questionnaire.md, section 4 : après trois tentatives infructueuses, un bandeau
// persistant invite à ne pas fermer l'onglet — la réponse reste en mémoire, pas dans
// localStorage (données d'entreprise, cf. règle de sécurité). role="alert" plutôt que
// role="status" : contrairement au statut par question, celui-ci doit interrompre le lecteur
// d'écran, la perte de données étant le risque que la section 4 qualifie de plus grave.
export function SaveStatusBanner({ hasError, onRetry }: SaveStatusBannerProps) {
  if (!hasError) {
    return null
  }

  return (
    <div role="alert" className="mb-4 rounded-card border border-red bg-white p-4 text-sm text-red shadow-card">
      <p>
        Certaines réponses n'ont pas pu être enregistrées. Merci de ne pas fermer l'onglet : elles restent en
        mémoire et seront à nouveau envoyées.
      </p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-2 rounded-button border border-red px-3 py-1 font-medium text-red hover:bg-red hover:text-white"
      >
        Réessayer
      </button>
    </div>
  )
}
