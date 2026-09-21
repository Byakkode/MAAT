import { BarChart2 } from 'lucide-react'
import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { ActionPlanCard } from '../components/dashboard/ActionPlanCard'
import { DomainRadarChart } from '../components/dashboard/DomainRadarChart'
import { DomainScoreTable } from '../components/dashboard/DomainScoreTable'
import { EvolutionChart } from '../components/dashboard/EvolutionChart'
import { InProgressBanner } from '../components/dashboard/InProgressBanner'
import { KpiCard } from '../components/dashboard/KpiCard'
import { ScoreSummaryCard } from '../components/dashboard/ScoreSummaryCard'
import { ReportDownloadButton } from '../components/report/ReportDownloadButton'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { SkeletonCard } from '../components/ui/Skeleton'
import { buttonLinkClass } from '../components/ui/buttonStyles'
import { DOMAIN_LABELS } from '../types/questionnaire'
import { useAuthStore } from '../store/authStore'
import { useDashboardStore } from '../store/dashboardStore'
import { getScoreLabel, roundScoreForDisplay } from '../constants/scoreLabels'

/* Labels courts pour les titres de cartes KPI — les DOMAIN_LABELS complets
   sont trop longs pour tenir confortablement dans une cellule text-2xl. */
const DOMAIN_SHORT: Record<string, string> = {
  Environmental: 'Environnement',
  Social: 'Social',
  Ethics: 'Éthique',
  Procurement: 'Achats',
  Governance: 'Gouvernance',
}

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
      <div className="flex flex-col gap-4">
        <PageHeader title="Tableau de bord" />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {[0, 1, 2, 3].map((i) => (
            <SkeletonCard key={i} />
          ))}
        </div>
        <div className="grid grid-cols-1 gap-4 xl:grid-cols-[3fr_2fr]">
          <SkeletonCard lines={7} />
          <SkeletonCard lines={7} />
        </div>
        <div className="grid grid-cols-1 gap-4 xl:grid-cols-[2fr_1fr]">
          <SkeletonCard lines={4} />
          <SkeletonCard lines={6} />
        </div>
      </div>
    )
  }

  if (loadStatus === 'error') {
    return (
      <div className="flex flex-col gap-4">
        <PageHeader title="Tableau de bord" />
        <Card>
          <p role="alert" className="text-sm text-red">
            {loadError}
          </p>
        </Card>
      </div>
    )
  }

  if (!hasCompletedDiagnostic) {
    return (
      <div className="flex flex-col gap-4">
        <PageHeader title="Tableau de bord" />
        {inProgressDiagnostic && <InProgressBanner diagnostic={inProgressDiagnostic} />}
        <Card as="section" className="flex flex-col items-center py-12 text-center">
          <BarChart2 className="mb-4 h-12 w-12 text-border" aria-hidden />
          <h2 className="mb-2 text-lg font-semibold text-text">
            {inProgressDiagnostic
              ? 'Votre tableau de bord vous attend'
              : 'Bienvenue sur votre tableau de bord'}
          </h2>
          <p className="mb-6 max-w-sm text-sm text-text-muted">
            {inProgressDiagnostic
              ? "Terminez votre questionnaire en cours pour voir apparaître ici votre score, votre radar et votre plan d'actions."
              : "Lancez votre questionnaire RSE pour obtenir votre score, identifier vos points forts et recevoir un plan d'actions personnalisé."}
          </p>
          {!inProgressDiagnostic && (
            <Link to="/questionnaire" className={buttonLinkClass()}>
              Commencer le questionnaire
            </Link>
          )}
        </Card>
      </div>
    )
  }

  if (!latestDiagnostic) return null

  const rounded = roundScoreForDisplay(latestDiagnostic.globalScore)
  const scoreLabel = getScoreLabel(rounded)
  const scoreDelta = history.length >= 2
    ? (history[history.length - 1]!.deltaFromPrevious ??
        history[history.length - 1]!.globalScore - history[history.length - 2]!.globalScore)
    : null

  const sortedByScore = [...domainScores].sort((a, b) => b.score - a.score)
  const strongest = sortedByScore[0]
  const weakest = sortedByScore[sortedByScore.length - 1]

  return (
    <div className="flex flex-col gap-4">
      {/* section 7 : le diagnostic en cours est un bandeau, jamais une page de substitution — il
          coexiste avec le reste de l'écran, complété ou non. */}
      {inProgressDiagnostic && <InProgressBanner diagnostic={inProgressDiagnostic} />}

      <div className="flex items-start justify-between gap-4">
        <PageHeader
          eyebrow={`Secteur ${latestDiagnostic.sectorCode}`}
          title="Tableau de bord"
          subtitle={`Dernier diagnostic — ${new Date(latestDiagnostic.completedAt ?? '').toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })}`}
        />
        {/* docs/specs/rapport-pdf.md, section 6 : bouton de téléchargement sur le tableau
            de bord, en plus de la page de résultat (RapportPage). */}
        <ReportDownloadButton diagnosticId={latestDiagnostic.id} />
      </div>

      {/* Carte hero — score global */}
      <ScoreSummaryCard
        score={rounded}
        scoreLabel={scoreLabel}
        sectorCode={latestDiagnostic.sectorCode}
        completedAt={latestDiagnostic.completedAt}
        delta={scoreDelta}
      />

      {/* Ligne 1 — KPI secondaires */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        {strongest && (
          <KpiCard
            title="Point fort"
            value={DOMAIN_SHORT[strongest.domain] ?? DOMAIN_LABELS[strongest.domain]}
            subtitle={`${Math.round(strongest.score)} / 100`}
            accent="green"
            delay={0}
          />
        )}
        {weakest && weakest.domain !== strongest?.domain && (
          <KpiCard
            title="À renforcer"
            value={DOMAIN_SHORT[weakest.domain] ?? DOMAIN_LABELS[weakest.domain]}
            subtitle={`${Math.round(weakest.score)} / 100`}
            accent="amber"
            delay={0.08}
          />
        )}
        <KpiCard
          title="Plan d'actions"
          value={`${actionPlan.completedCount} / ${actionPlan.totalCount}`}
          subtitle="actions terminées"
          accent="blue"
          delay={0.16}
        />
      </div>

      {/* Ligne 2 — Radar + Tableau */}
      <div className="grid grid-cols-1 gap-4 xl:grid-cols-[3fr_2fr]">
        <Card as="section">
          <DomainRadarChart domainScores={domainScores} />
          <div className="mt-5 border-t border-border pt-4">
            <DomainScoreTable domainScores={domainScores} />
          </div>
        </Card>

        {/* Ligne 3 — Évolution + Plan d'actions (empilés à droite du radar sur grand écran) */}
        <div className="flex flex-col gap-4">
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
        </div>
      </div>
    </div>
  )
}
