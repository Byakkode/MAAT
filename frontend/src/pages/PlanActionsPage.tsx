import { ClipboardList, SlidersHorizontal, X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { EFFORT_LABELS } from '../constants/effortLabels'
import { useAuthStore } from '../store/authStore'
import { useDashboardStore } from '../store/dashboardStore'
import { usePlanActionsStore } from '../store/planActionsStore'
import { DOMAIN_LABELS } from '../types/questionnaire'
import type { RseDomain } from '../types/questionnaire'
import type { EffortLevel } from '../types/dashboard'
import { Badge, type BadgeVariant } from '../components/ui/Badge'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { buttonLinkClass } from '../components/ui/buttonStyles'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })
}

function pluralize(count: number, singular: string, plural: string): string {
  return count > 1 ? plural : singular
}

const EFFORT_BADGE_VARIANT: Record<EffortLevel, BadgeVariant> = {
  Low: 'green',
  Medium: 'amber',
  High: 'red',
}

const ALL_EFFORTS: EffortLevel[] = ['Low', 'Medium', 'High']
const ALL_DOMAINS: RseDomain[] = ['Environmental', 'Social', 'Ethics', 'Procurement', 'Governance']

const EFFORT_CHIP_ACTIVE: Record<EffortLevel, string> = {
  Low:    'border-chart-environnement/50 bg-chart-environnement/10 text-chart-environnement',
  Medium: 'border-orange/50 bg-orange/10 text-orange',
  High:   'border-red/50 bg-red/10 text-red',
}

const DOMAIN_CHIP_ACTIVE: Record<RseDomain, string> = {
  Environmental: 'border-chart-environnement/50 bg-chart-environnement/10 text-chart-environnement',
  Social:        'border-chart-social/50 bg-chart-social/10 text-chart-social',
  Ethics:        'border-chart-ethique/50 bg-chart-ethique/10 text-chart-ethique',
  Procurement:   'border-chart-achats/50 bg-chart-achats/10 text-chart-achats',
  Governance:    'border-chart-gouvernance/50 bg-chart-gouvernance/10 text-chart-gouvernance',
}

const CHIP_BASE = 'rounded-full border px-2.5 py-1 text-[12px] font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-maat/40'
const CHIP_OFF  = 'border-border bg-white text-text-muted hover:border-border-strong hover:text-text'

function chipCls(active: boolean, activeClass: string): string {
  return `${CHIP_BASE} ${active ? activeClass : CHIP_OFF}`
}

// docs/specs/recommandations.md, section 4 : destination du lien "Voir tout le plan d'actions"
// (ActionPlanCard) et de l'entrée de navigation "Plan d'actions" (coquille-et-compte.md,
// section 3). N'existait jusqu'ici qu'à l'état de placeholder — sans appel réseau, sans
// résolution d'identifiant de diagnostic, l'écran ne pouvait rien afficher.
export function PlanActionsPage() {
  const dashboardLoadStatus = useDashboardStore((s) => s.loadStatus)
  const hasCompletedDiagnostic = useDashboardStore((s) => s.hasCompletedDiagnostic)
  const latestDiagnosticId = useDashboardStore((s) => s.latestDiagnostic?.id)
  const loadDashboard = useDashboardStore((s) => s.load)

  const loadStatus = usePlanActionsStore((s) => s.loadStatus)
  const loadError = usePlanActionsStore((s) => s.loadError)
  const items = usePlanActionsStore((s) => s.items)
  const togglingCode = usePlanActionsStore((s) => s.togglingCode)
  const toggleError = usePlanActionsStore((s) => s.toggleError)
  const load = usePlanActionsStore((s) => s.load)
  const toggle = usePlanActionsStore((s) => s.toggle)

  const role = useAuthStore((s) => s.user?.role)
  const canEdit = role !== 'Viewer'

  // Filtres — appelés ici (avant les early returns) pour respecter les règles des hooks React.
  const [effortFilter, setEffortFilter] = useState<Set<EffortLevel>>(() => new Set())
  const [domainFilter, setDomainFilter] = useState<Set<RseDomain>>(() => new Set())
  const [minImpact, setMinImpact] = useState(0)

  // Seuils d'impact calculés depuis les données réelles (percentiles 50 et 75).
  const impactThresholds = useMemo<number[]>(() => {
    if (items.length < 2) return []
    const sorted = [...items].map((i) => i.impactPoints).sort((a, b) => a - b)
    const p50 = sorted[Math.floor(sorted.length * 0.5)]
    const p75 = sorted[Math.floor(sorted.length * 0.75)]
    return [...new Set([p50, p75])].filter((v) => v > 0)
  }, [items])

  const filteredItems = useMemo(() => {
    return items.filter((item) => {
      if (effortFilter.size > 0 && !effortFilter.has(item.effortLevel)) return false
      if (domainFilter.size > 0 && !domainFilter.has(item.domain)) return false
      if (item.impactPoints < minImpact) return false
      return true
    })
  }, [items, effortFilter, domainFilter, minImpact])

  const hasActiveFilters = effortFilter.size > 0 || domainFilter.size > 0 || minImpact > 0

  function toggleEffort(level: EffortLevel) {
    setEffortFilter((prev) => {
      const next = new Set(prev)
      if (next.has(level)) { next.delete(level) } else { next.add(level) }
      return next
    })
  }

  function toggleDomain(domain: RseDomain) {
    setDomainFilter((prev) => {
      const next = new Set(prev)
      if (next.has(domain)) { next.delete(domain) } else { next.add(domain) }
      return next
    })
  }

  function resetFilters() {
    setEffortFilter(new Set())
    setDomainFilter(new Set())
    setMinImpact(0)
  }

  // AppShell ne charge useDashboardStore qu'une fois par session (idle-guardé, pour le badge
  // d'avancement de la barre latérale) : un diagnostic complété entre-temps depuis un autre
  // écran laisserait hasCompletedDiagnostic/latestDiagnostic périmés si cet écran s'y fiait
  // sans recharger — un compte venant de terminer son questionnaire, puis naviguant
  // directement ici sans passer par le tableau de bord, verrait l'invitation à démarrer un
  // diagnostic au lieu de son plan d'actions. Rechargement inconditionnel au montage, même
  // convention que DashboardPage.
  useEffect(() => {
    void loadDashboard()
  }, [loadDashboard])

  useEffect(() => {
    if (latestDiagnosticId) {
      void load(latestDiagnosticId)
    }
  }, [latestDiagnosticId, load])

  if (dashboardLoadStatus === 'idle' || dashboardLoadStatus === 'loading') {
    return (
      <p role="status" className="text-text-muted">
        Chargement du plan d&apos;actions…
      </p>
    )
  }

  if (dashboardLoadStatus === 'error') {
    return (
      <p role="alert" className="text-red">
        Impossible de récupérer votre plan d&apos;actions.
      </p>
    )
  }

  if (!hasCompletedDiagnostic || !latestDiagnosticId) {
    return (
      <div className="flex flex-col gap-4">
        <PageHeader title="Plan d'actions" />
        <Card as="section" className="flex flex-col items-center py-12 text-center">
          <ClipboardList className="mb-4 h-12 w-12 text-border" aria-hidden />
          <h2 className="mb-2 text-lg font-semibold text-text">Aucun plan disponible</h2>
          <p className="mb-6 max-w-sm text-sm text-text-muted">
            Vous n&apos;avez pas encore de diagnostic complété. Terminez votre questionnaire RSE pour voir apparaître
            votre plan d&apos;actions.
          </p>
          <Link to="/questionnaire" className={buttonLinkClass()}>
            Commencer le questionnaire
          </Link>
        </Card>
      </div>
    )
  }

  if (loadStatus === 'idle' || loadStatus === 'loading') {
    return (
      <p role="status" className="text-text-muted">
        Chargement du plan d&apos;actions…
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

  // La barre de progression porte sur la totalité du plan, indépendamment des filtres actifs.
  const completedCount = items.filter((item) => item.isCompleted).length
  const progressPercent = items.length > 0 ? Math.round((completedCount / items.length) * 100) : 0

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Plan d'actions" />

      {/* Barre de progression globale */}
      {items.length > 0 && (
        <Card>
          <div className="mb-3 flex items-center justify-between gap-4">
            <div>
              <p className="text-[13px] font-semibold text-text tabular-nums lining-nums">
                <span className="text-green-maat-text">{completedCount}</span>
                <span className="text-text-muted"> / {items.length} actions</span>
              </p>
              <p className="text-[12px] text-text-muted">
                {items.length - completedCount}{' '}
                {pluralize(items.length - completedCount, 'action restante', 'actions restantes')}
              </p>
            </div>
            <strong className="text-[2rem] font-bold tabular-nums text-text" aria-hidden="true">
              {progressPercent}&nbsp;%
            </strong>
          </div>
          <div className="h-1.5 w-full overflow-hidden rounded-full bg-border">
            <div
              className="h-full rounded-full bg-green-maat transition-all duration-500"
              style={{ width: `${progressPercent}%` }}
              role="progressbar"
              aria-valuenow={progressPercent}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-label={`${progressPercent} % des actions terminées`}
            />
          </div>
        </Card>
      )}

      {/* Panneau de filtres */}
      {items.length > 0 && (
        <Card>
          <div className="mb-4 flex items-center justify-between gap-2">
            <div className="flex items-center gap-2">
              <SlidersHorizontal size={14} className="text-text-muted" aria-hidden="true" />
              <span className="text-[13px] font-semibold text-text">Filtres</span>
              {hasActiveFilters && (
                <span className="flex h-4 min-w-[16px] items-center justify-center rounded-full bg-blue-maat px-1 text-[10px] font-bold text-white tabular-nums">
                  {effortFilter.size + domainFilter.size + (minImpact > 0 ? 1 : 0)}
                </span>
              )}
            </div>
            {hasActiveFilters && (
              <button
                type="button"
                onClick={resetFilters}
                className="flex items-center gap-1 rounded-full border border-border px-2.5 py-1 text-[12px] font-medium text-text-muted transition-colors hover:border-border-strong hover:text-text"
              >
                <X size={11} aria-hidden="true" />
                Réinitialiser
              </button>
            )}
          </div>

          <div className="flex flex-col gap-3">
            {/* Effort */}
            <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
              <span className="w-16 shrink-0 text-[11px] font-semibold uppercase tracking-[0.08em] text-text-muted">
                Effort
              </span>
              <div className="flex flex-wrap gap-1.5">
                {ALL_EFFORTS.map((level) => (
                  <button
                    key={level}
                    type="button"
                    aria-pressed={effortFilter.has(level)}
                    onClick={() => toggleEffort(level)}
                    className={chipCls(effortFilter.has(level), EFFORT_CHIP_ACTIVE[level])}
                  >
                    {EFFORT_LABELS[level].replace('Effort ', '')}
                  </button>
                ))}
              </div>
            </div>

            {/* Domaine */}
            <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
              <span className="w-16 shrink-0 text-[11px] font-semibold uppercase tracking-[0.08em] text-text-muted">
                Thème
              </span>
              <div className="flex flex-wrap gap-1.5">
                {ALL_DOMAINS.map((domain) => (
                  <button
                    key={domain}
                    type="button"
                    aria-pressed={domainFilter.has(domain)}
                    onClick={() => toggleDomain(domain)}
                    className={chipCls(domainFilter.has(domain), DOMAIN_CHIP_ACTIVE[domain])}
                  >
                    {DOMAIN_LABELS[domain]}
                  </button>
                ))}
              </div>
            </div>

            {/* Impact minimum — affiché seulement si les données produisent des seuils distincts */}
            {impactThresholds.length > 0 && (
              <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
                <span className="w-16 shrink-0 text-[11px] font-semibold uppercase tracking-[0.08em] text-text-muted">
                  Impact
                </span>
                <div className="flex flex-wrap gap-1.5">
                  <button
                    type="button"
                    aria-pressed={minImpact === 0}
                    onClick={() => setMinImpact(0)}
                    className={chipCls(minImpact === 0, 'border-blue-maat/50 bg-blue-maat/10 text-blue-maat')}
                  >
                    Tous
                  </button>
                  {impactThresholds.map((threshold) => (
                    <button
                      key={threshold}
                      type="button"
                      aria-pressed={minImpact === threshold}
                      onClick={() => setMinImpact(minImpact === threshold ? 0 : threshold)}
                      className={chipCls(minImpact === threshold, 'border-blue-maat/50 bg-blue-maat/10 text-blue-maat')}
                    >
                      ≥&nbsp;{threshold}&nbsp;pts
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>
        </Card>
      )}

      {/* Liste des actions */}
      {items.length === 0 ? (
        // docs/specs/recommandations.md, section 4 : "Aucune recommandation déclenchée est un
        // résultat valide, pas une erreur [...] laisser le frontend afficher un message de
        // félicitation." Jamais un écran blanc pour ce cas.
        <Card>
          <p className="text-text-muted">
            Aucune recommandation déclenchée pour ce diagnostic. Bravo : votre démarche RSE est déjà mature sur
            l&apos;ensemble des points évalués.
          </p>
        </Card>
      ) : filteredItems.length === 0 ? (
        <Card className="flex flex-col items-center py-10 text-center">
          <p className="mb-3 text-[13px] font-medium text-text">Aucune action ne correspond à ces filtres.</p>
          <button
            type="button"
            onClick={resetFilters}
            className="rounded-xl border border-border bg-white px-4 py-2 text-[13px] font-medium text-text shadow-card transition-colors hover:border-border-strong hover:bg-bg"
          >
            Réinitialiser les filtres
          </button>
        </Card>
      ) : (
        <>
          {hasActiveFilters && (
            <p className="text-[12px] text-text-muted" aria-live="polite">
              {filteredItems.length}{' '}
              {pluralize(filteredItems.length, 'action affichée', 'actions affichées')} sur {items.length}
            </p>
          )}
          <ul className="flex flex-col gap-2.5">
            {filteredItems.map((item) => (
              <li
                key={item.code}
                className={`rounded-xl border border-border bg-white p-4 shadow-card transition-all duration-150 hover:border-border-strong hover:shadow-card-hover ${
                  item.isCompleted ? 'opacity-60' : ''
                }`}
              >
                <div className="flex items-start gap-3">
                  <input
                    type="checkbox"
                    id={`plan-actions-${item.code}`}
                    checked={item.isCompleted}
                    disabled={!canEdit || togglingCode === item.code}
                    onChange={(event) => void toggle(item.code, event.target.checked)}
                    className="mt-1 h-4 w-4 shrink-0 accent-blue-maat"
                  />
                  <label htmlFor={`plan-actions-${item.code}`} className="flex-1 cursor-pointer space-y-1.5">
                    <span
                      className={`block text-[13.5px] font-medium leading-snug ${
                        item.isCompleted ? 'text-text-muted line-through' : 'text-text'
                      }`}
                    >
                      {item.actionText}
                    </span>

                    <span className="block text-[12.5px] leading-relaxed text-text-muted">{item.detailText}</span>

                    {/* Badges visuels — aria-hidden pour ne pas interférer avec les tests */}
                    <span className="flex flex-wrap gap-1.5 pt-0.5" aria-hidden="true">
                      <Badge domain={item.domain}>{DOMAIN_LABELS[item.domain]}</Badge>
                      <Badge variant={EFFORT_BADGE_VARIANT[item.effortLevel]}>
                        {EFFORT_LABELS[item.effortLevel]}
                      </Badge>
                      <Badge variant="default">{item.impactPoints} pts d&apos;impact</Badge>
                    </span>

                    {/* Texte structuré pour les tests (getByText) et les lecteurs d'écran */}
                    <span className="sr-only">
                      {DOMAIN_LABELS[item.domain]} · {EFFORT_LABELS[item.effortLevel]} ·{' '}
                      {item.impactPoints} points d&apos;impact
                    </span>

                    {item.isCompleted && item.completedAt && (
                      <span className="block text-[11.5px] font-medium text-green-maat-text tabular-nums lining-nums">
                        Terminée le {formatDate(item.completedAt)}
                      </span>
                    )}
                  </label>
                </div>
              </li>
            ))}
          </ul>
        </>
      )}

      {toggleError && (
        <p role="alert" className="text-sm text-red">
          {toggleError}
        </p>
      )}

      {items.length > 0 && (
        <p className="text-xs text-text-muted">
          Cocher une action ne modifie pas le score : elle est prise en compte lors de votre prochain diagnostic.
        </p>
      )}
    </div>
  )
}
