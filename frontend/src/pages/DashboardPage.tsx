import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { ActionPlanCard } from '../components/dashboard/ActionPlanCard'
import { DomainRadarChart } from '../components/dashboard/DomainRadarChart'
import { DomainScoreTable } from '../components/dashboard/DomainScoreTable'
import { EvolutionChart } from '../components/dashboard/EvolutionChart'
import { InProgressBanner } from '../components/dashboard/InProgressBanner'
import { ScoreSummary } from '../components/dashboard/ScoreSummary'
import { ReportDownloadButton } from '../components/report/ReportDownloadButton'
import { useAuthStore } from '../store/authStore'
import { useDashboardStore } from '../store/dashboardStore'

// docs/specs/dashboard.md, section 1 : trois états traités comme des écrans à part entière —
// aucun diagnostic, diagnostic en cours seul, au moins un complété — jamais comme des cas
// d'erreur. Un seul appel réseau au montage (section 8, "un seul appel réseau, un seul état
// de chargement") : useDashboardStore.load() ci-dessous, jamais un fetch par section.
export function DashboardPage() {
  const load = useDashboardStore((s) => s.load)
  const loadStatus = useDashboardStore((s) => s.loadStatus)
  const loadError = useDashboardStore((s) => s.loadError)
  const hasCompletedDiagnostic = useDashboardStore((s) => s.hasCompletedDiagnostic)
  const latestDiagnostic = useDashboardStore((s) => s.latestDiagnostic)
  const domainScores = useDashboardStore((s) => s.domainScores)
  const history = useDashboardStore((s) => s.history)
  const actionPlan = useDashboardStore((s) => s.actionPlan)
  const inProgressDiagnostic = useDashboardStore((s) => s.inProgressDiagnostic)
  const togglingCode = useDashboardStore((s) => s.togglingCode)
  const toggleError = useDashboardStore((s) => s.toggleError)
  const toggleRecommendation = useDashboardStore((s) => s.toggleRecommendation)

  // section 6 : cases actionnables pour Admin et User, lecture seule pour Viewer (cas 19).
  const role = useAuthStore((s) => s.user?.role)
  const canEditActionPlan = role !== 'Viewer'

  useEffect(() => {
    void load()
  }, [load])

  if (loadStatus === 'idle' || loadStatus === 'loading') {
    return (
      <p role="status" className="text-text-muted">
        Chargement du tableau de bord…
      </p>
    )
  }

  if (loadStatus === 'error') {
    return (
      <p role="alert" className="text-red">
        {loadError}
      </p>
    )
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="sr-only">Tableau de bord</h1>

      {/* section 7 : le diagnostic en cours est un bandeau, jamais une page de substitution —
          il coexiste avec le reste de l'écran, complété ou non. */}
      {inProgressDiagnostic && <InProgressBanner diagnostic={inProgressDiagnostic} />}

      {!hasCompletedDiagnostic ? (
        <section className="rounded-card border border-border bg-white p-5 shadow-card">
          <h2 className="mb-2 text-xl font-semibold text-text">
            {inProgressDiagnostic ? 'Votre tableau de bord' : 'Bienvenue sur votre tableau de bord'}
          </h2>
          {inProgressDiagnostic ? (
            <p className="text-text-muted">
              Terminez votre questionnaire en cours pour voir apparaître ici votre score, votre radar et votre plan
              d&apos;actions.
            </p>
          ) : (
            <>
              <p className="mb-3 text-text-muted">
                Vous n&apos;avez pas encore réalisé de diagnostic. Lancez votre questionnaire RSE pour découvrir votre
                score.
              </p>
              <Link
                to="/questionnaire"
                className="inline-block rounded-button bg-blue-maat px-4 py-2 font-medium text-white shadow-button"
              >
                Commencer le questionnaire
              </Link>
            </>
          )}
        </section>
      ) : (
        latestDiagnostic && (
          <>
            <ScoreSummary score={latestDiagnostic.globalScore} sectorCode={latestDiagnostic.sectorCode} />

            {/* docs/specs/rapport-pdf.md, section 6 : bouton de téléchargement sur le tableau
                de bord, en plus de la page de résultat (RapportPage). */}
            <ReportDownloadButton diagnosticId={latestDiagnostic.id} />

            <section className="rounded-card border border-border bg-white p-5 shadow-card">
              <DomainRadarChart domainScores={domainScores} />
              <div className="mt-4">
                <DomainScoreTable domainScores={domainScores} />
              </div>
            </section>

            <EvolutionChart history={history} />

            <ActionPlanCard
              actionPlan={actionPlan}
              canEdit={canEditActionPlan}
              togglingCode={togglingCode}
              onToggle={(code, isCompleted) => void toggleRecommendation(latestDiagnostic.id, code, isCompleted)}
            />
            {toggleError && (
              <p role="alert" className="text-sm text-red">
                {toggleError}
              </p>
            )}
          </>
        )
      )}
    </div>
  )
}
