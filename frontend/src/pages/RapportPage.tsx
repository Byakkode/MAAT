import { useEffect } from 'react'
import { ReportDownloadButton } from '../components/report/ReportDownloadButton'
import { useDashboardStore } from '../store/dashboardStore'

// docs/specs/rapport-pdf.md, section 6 : bouton de téléchargement sur la page de résultat
// d'un diagnostic. Réutilise useDashboardStore (déjà chargé dans le parcours normal depuis
// DashboardPage) plutôt qu'un appel réseau dédié : latestDiagnostic porte déjà l'identifiant
// du diagnostic dont ce rapport est le compte-rendu.
export function RapportPage() {
  const load = useDashboardStore((s) => s.load)
  const loadStatus = useDashboardStore((s) => s.loadStatus)
  const loadError = useDashboardStore((s) => s.loadError)
  const latestDiagnostic = useDashboardStore((s) => s.latestDiagnostic)

  useEffect(() => {
    void load()
  }, [load])

  return (
    <section className="rounded-card border border-border bg-white p-5 shadow-card">
      <h1 className="mb-4 text-2xl font-semibold text-text">Rapport</h1>

      {(loadStatus === 'idle' || loadStatus === 'loading') && (
        <p role="status" className="text-text-muted">
          Chargement…
        </p>
      )}

      {loadStatus === 'error' && (
        <p role="alert" className="text-red">
          {loadError}
        </p>
      )}

      {loadStatus === 'loaded' &&
        (latestDiagnostic ? (
          <ReportDownloadButton diagnosticId={latestDiagnostic.id} />
        ) : (
          <p className="text-text-muted">
            Aucun diagnostic complété pour le moment : terminez le questionnaire pour générer votre premier rapport.
          </p>
        ))}
    </section>
  )
}
