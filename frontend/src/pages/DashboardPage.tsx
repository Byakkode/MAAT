import { BarChart2 } from 'lucide-react'
import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { ActionPlanCard } from '../components/dashboard/ActionPlanCard'
import { DomainRadarChart } from '../components/dashboard/DomainRadarChart'
import { DomainScoreCard } from '../components/dashboard/DomainScoreCard'
import { IndicatorsTrendCard } from '../components/dashboard/IndicatorsTrendCard'
import { DomainScoreTable } from '../components/dashboard/DomainScoreTable'
import { EvolutionChart } from '../components/dashboard/EvolutionChart'
import { InProgressBanner } from '../components/dashboard/InProgressBanner'
import { KpiCard } from '../components/dashboard/KpiCard'
import { ReportDownloadButton } from '../components/report/ReportDownloadButton'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { SkeletonCard } from '../components/ui/Skeleton'
import { buttonLinkClass } from '../components/ui/buttonStyles'
import { DOMAIN_ORDER } from '../types/questionnaire'
import type { SectorBenchmark } from '../types/dashboard'
import { useAuthStore } from '../store/authStore'
import { useDashboardStore } from '../store/dashboardStore'
import { getScoreLabel, roundScoreForDisplay } from '../constants/scoreLabels'

// docs/specs/dashboard.md, section 1 : trois états traités comme des écrans à part entière.
// Un seul appel réseau au montage (section 8).
export function DashboardPage() {
  const load = useDashboardStore((s) => s.load)
  const loadStatus = useDashboardStore((s) => s.loadStatus)
  const loadError = useDashboardStore((s) => s.loadError)
  const hasCompletedDiagnostic = useDashboardStore((s) => s.hasCompletedDiagnostic)
  const latestDiagnostic = useDashboardStore((s) => s.latestDiagnostic)
  const domainScores = useDashboardStore((s) => s.domainScores)
  const history = useDashboardStore((s) => s.history)
  const benchmark = useDashboardStore((s) => s.benchmark)
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
        <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
          {[0, 1, 2, 3].map((i) => <SkeletonCard key={i} />)}
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3 xl:grid-cols-5">
          {[0, 1, 2, 3, 4].map((i) => <SkeletonCard key={i} lines={3} />)}
        </div>
        <div className="grid grid-cols-1 gap-4 xl:grid-cols-[3fr_2fr]">
          <SkeletonCard lines={7} />
          <div className="flex flex-col gap-4">
            <SkeletonCard lines={4} />
            <SkeletonCard lines={5} />
          </div>
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

  const progressPercent =
    actionPlan.totalCount > 0
      ? Math.round((actionPlan.completedCount / actionPlan.totalCount) * 100)
      : 0

  const orderedDomainScores = DOMAIN_ORDER
    .map((domain) => domainScores.find((s) => s.domain === domain))
    .filter(Boolean) as typeof domainScores

  return (
    <div className="flex flex-col gap-4">
      {/* section 7 : bandeau diagnostic en cours */}
      {inProgressDiagnostic && <InProgressBanner diagnostic={inProgressDiagnostic} />}

      {/* En-tête */}
      <div className="flex flex-wrap items-start justify-between gap-4">
        <PageHeader
          eyebrow={`Secteur ${latestDiagnostic.sectorCode}`}
          title="Tableau de bord"
          subtitle={`Dernier diagnostic · ${new Date(latestDiagnostic.completedAt ?? '').toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })}`}
        />
        <ReportDownloadButton diagnosticId={latestDiagnostic.id} />
      </div>

      {/* Ligne 1 : 4 indicateurs clés */}
      <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
        <KpiCard
          title="Score RSE global"
          value={String(rounded)}
          unit="/ 100"
          subtitle={scoreLabel}
          delta={scoreDelta}
          accent="blue"
          delay={0}
        />

        <BenchmarkTile benchmark={benchmark} sectorCode={latestDiagnostic.sectorCode} />

        <KpiCard
          title="Plan d'actions"
          value={`${progressPercent}%`}
          subtitle={`${actionPlan.completedCount} / ${actionPlan.totalCount} terminées`}
          accent={progressPercent >= 70 ? 'green' : progressPercent >= 40 ? 'amber' : 'neutral'}
          delay={0.16}
        />

        <KpiCard
          title="Diagnostics réalisés"
          value={String(history.length)}
          subtitle={history.length > 1 ? 'depuis le démarrage' : 'Premier diagnostic'}
          accent="neutral"
          delay={0.24}
        />
      </div>

      {/* Ligne 2 : scores par domaine */}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3 xl:grid-cols-5">
        {orderedDomainScores.map((ds, i) => (
          <DomainScoreCard
            key={ds.domain}
            domainScore={ds}
            delay={0.05 * i}
          />
        ))}
      </div>

      {/* Ligne 3 : radar + table | évolution + plan d'actions */}
      <div className="grid grid-cols-1 gap-4 xl:grid-cols-[3fr_2fr]">
        <Card as="section">
          <DomainRadarChart domainScores={domainScores} />
          <div className="mt-5 border-t border-border pt-4">
            <DomainScoreTable domainScores={domainScores} />
          </div>
        </Card>

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

      {/* Ligne 4 : évolution des indicateurs RSE quantitatifs */}
      <IndicatorsTrendCard />
    </div>
  )
}

// Tuile benchmark : affiche le percentile sectoriel si disponible, sinon explique pourquoi.
function BenchmarkTile({ benchmark, sectorCode }: { benchmark: SectorBenchmark | null; sectorCode: string }) {
  if (!benchmark) {
    return (
      <KpiCard
        title="Benchmark sectoriel"
        value="N/A"
        subtitle="Non encore calculé"
        accent="neutral"
        delay={0.08}
      />
    )
  }
  if (!benchmark.available) {
    return (
      <KpiCard
        title="Benchmark sectoriel"
        value="N/A"
        subtitle={benchmark.reason ?? `Seuil non atteint pour ${sectorCode}`}
        accent="neutral"
        delay={0.08}
      />
    )
  }
  const topPct = 100 - Math.round(benchmark.percentile!)
  return (
    <KpiCard
      title="Benchmark sectoriel"
      value={`Top ${topPct}%`}
      subtitle={`sur ${benchmark.sampleSize} entreprises`}
      accent="green"
      delay={0.08}
    />
  )
}
