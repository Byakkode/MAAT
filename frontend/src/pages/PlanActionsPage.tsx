import { ChevronDown, ChevronUp, ClipboardList, SlidersHorizontal, X } from 'lucide-react'
import { memo, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import * as actionPlanApi from '../api/actionPlanApi'
import type { ActionItemStatus, ActionItemWithProgress, UpsertPayload } from '../api/actionPlanApi'
import { EFFORT_LABELS } from '../constants/effortLabels'
import { useAuthStore } from '../store/authStore'
import { useDashboardStore } from '../store/dashboardStore'
import { DOMAIN_LABELS, DOMAIN_ORDER } from '../types/questionnaire'
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

// ─── Statuts ──────────────────────────────────────────────────────────────────

const STATUS_ORDER: ActionItemStatus[] = ['Planned', 'InProgress', 'Blocked', 'Done']

const STATUS_CONFIG: Record<ActionItemStatus, { label: string; classes: string }> = {
  Planned:    { label: 'Planifié',  classes: 'border-border bg-bg text-text-muted hover:border-border-strong' },
  InProgress: { label: 'En cours',  classes: 'border-blue-maat/40 bg-blue-maat/10 text-blue-maat-text hover:bg-blue-maat/15' },
  Blocked:    { label: 'Bloqué',    classes: 'border-orange/40 bg-orange/10 text-orange hover:bg-orange/15' },
  Done:       { label: 'Terminé',   classes: 'border-green-maat/40 bg-green-maat/10 text-green-maat-text hover:bg-green-maat/15' },
}

// ─── Carte d'action expandable ────────────────────────────────────────────────

interface ActionItemCardProps {
  item: ActionItemWithProgress
  canEdit: boolean
  onSave: (payload: UpsertPayload) => Promise<void>
}

function ActionItemCardComponent({ item, canEdit, onSave }: ActionItemCardProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [localNotes, setLocalNotes] = useState(item.notes ?? '')
  const [localAssignedTo, setLocalAssignedTo] = useState(item.assignedTo ?? '')
  const [localDueDate, setLocalDueDate] = useState(
    item.dueDate ? item.dueDate.slice(0, 10) : ''
  )
  const [isSaving, setIsSaving] = useState(false)
  const notesTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  // Ref pour éviter les closures périmées dans le debounce des notes.
  const itemRef = useRef(item)

  useEffect(() => {
    itemRef.current = item
    setLocalNotes(item.notes ?? '')
    setLocalAssignedTo(item.assignedTo ?? '')
    setLocalDueDate(item.dueDate ? item.dueDate.slice(0, 10) : '')
  }, [item])

  async function doSave(payload: UpsertPayload) {
    setIsSaving(true)
    try {
      await onSave(payload)
    } finally {
      setIsSaving(false)
    }
  }

  function handleStatusCycle() {
    const idx = STATUS_ORDER.indexOf(item.status)
    const next = STATUS_ORDER[(idx + 1) % STATUS_ORDER.length]!
    void doSave({
      status: next,
      assignedTo: item.assignedTo,
      dueDate: item.dueDate,
      notes: item.notes,
    })
  }

  function handleNotesChange(value: string) {
    setLocalNotes(value)
    if (notesTimerRef.current) clearTimeout(notesTimerRef.current)
    notesTimerRef.current = setTimeout(() => {
      const cur = itemRef.current
      void doSave({ status: cur.status, assignedTo: cur.assignedTo, dueDate: cur.dueDate, notes: value })
    }, 1200)
  }

  function handleAssignedToBlur() {
    const cur = itemRef.current
    if (localAssignedTo !== (cur.assignedTo ?? '')) {
      void doSave({
        status: cur.status,
        assignedTo: localAssignedTo || null,
        dueDate: cur.dueDate,
        notes: cur.notes,
      })
    }
  }

  function handleDueDateChange(value: string) {
    setLocalDueDate(value)
    const cur = itemRef.current
    void doSave({ status: cur.status, assignedTo: cur.assignedTo, dueDate: value || null, notes: cur.notes })
  }

  const isDone = item.status === 'Done'
  const { label: statusLabel, classes: statusClasses } = STATUS_CONFIG[item.status]
  const nextStatus = STATUS_ORDER[(STATUS_ORDER.indexOf(item.status) + 1) % STATUS_ORDER.length]!

  return (
    <li
      className={`rounded-xl border border-border bg-white shadow-card transition-all duration-150 ${
        isDone ? 'opacity-70' : 'hover:border-border-strong hover:shadow-card-hover'
      }`}
    >
      {/* Ligne principale */}
      <div className="flex items-start gap-3 p-4">
        {/* Badge de statut actionnable */}
        <button
          type="button"
          disabled={!canEdit || isSaving}
          onClick={handleStatusCycle}
          className={`mt-0.5 shrink-0 rounded-full border px-2.5 py-[3px] text-[11.5px] font-semibold transition-colors disabled:cursor-default disabled:opacity-50 ${statusClasses}`}
          aria-label={`Statut : ${statusLabel}. Cliquer pour passer à ${STATUS_CONFIG[nextStatus].label}`}
        >
          {statusLabel}
        </button>

        {/* Texte + badges */}
        <div className="min-w-0 flex-1">
          <p
            className={`text-[13.5px] font-medium leading-snug ${
              isDone ? 'text-text-muted line-through' : 'text-text'
            }`}
          >
            {item.actionText}
          </p>

          <span className="mt-1.5 flex flex-wrap gap-1.5" aria-hidden="true">
            <Badge domain={item.domain}>{DOMAIN_LABELS[item.domain]}</Badge>
            <Badge variant={EFFORT_BADGE_VARIANT[item.effortLevel]}>
              {EFFORT_LABELS[item.effortLevel]}
            </Badge>
            <Badge variant="default">{item.impactPoints} pts d&apos;impact</Badge>
          </span>

          {/* Texte lisible pour les tests (getByText) et les lecteurs d'écran */}
          <span className="sr-only">
            {DOMAIN_LABELS[item.domain]} · {EFFORT_LABELS[item.effortLevel]} ·{' '}
            {item.impactPoints} points d&apos;impact
          </span>

          {isDone && item.completedAt && (
            <p className="mt-1 text-[11.5px] font-medium text-green-maat-text">
              Terminée le {formatDate(item.completedAt)}
            </p>
          )}
        </div>

        {/* Bouton d'expansion */}
        {canEdit && (
          <button
            type="button"
            onClick={() => setIsOpen(!isOpen)}
            className="shrink-0 rounded-lg border border-border p-1.5 text-text-muted transition-colors hover:border-border-strong hover:text-text"
            aria-expanded={isOpen}
            aria-label={isOpen ? 'Réduire les détails' : 'Modifier les détails'}
          >
            {isOpen
              ? <ChevronUp size={14} strokeWidth={2} aria-hidden />
              : <ChevronDown size={14} strokeWidth={2} aria-hidden />}
          </button>
        )}
      </div>

      {/* Panneau de détails (expandable) */}
      {isOpen && (
        <div className="border-t border-border px-4 pb-4 pt-3">
          {item.detailText && (
            <p className="mb-3 text-[12.5px] leading-relaxed text-text-muted">{item.detailText}</p>
          )}

          <div className="mb-3 grid grid-cols-1 gap-3 sm:grid-cols-2">
            {/* Responsable */}
            <div>
              <label
                htmlFor={`assigned-${item.code}`}
                className="mb-1 block text-[11px] font-semibold uppercase tracking-[0.07em] text-text-muted"
              >
                Responsable
              </label>
              <input
                id={`assigned-${item.code}`}
                type="text"
                value={localAssignedTo}
                onChange={(e) => setLocalAssignedTo(e.target.value)}
                onBlur={handleAssignedToBlur}
                placeholder="Nom ou poste"
                className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-[13px] text-text outline-none placeholder:text-text-muted focus:border-blue-maat focus:ring-1 focus:ring-blue-maat/20"
              />
            </div>

            {/* Échéance */}
            <div>
              <label
                htmlFor={`due-${item.code}`}
                className="mb-1 block text-[11px] font-semibold uppercase tracking-[0.07em] text-text-muted"
              >
                Échéance
              </label>
              <input
                id={`due-${item.code}`}
                type="date"
                value={localDueDate}
                onChange={(e) => void handleDueDateChange(e.target.value)}
                className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-[13px] text-text outline-none focus:border-blue-maat focus:ring-1 focus:ring-blue-maat/20"
              />
            </div>
          </div>

          {/* Notes auto-sauvegardées */}
          <div>
            <div className="mb-1 flex items-center justify-between">
              <label
                htmlFor={`notes-${item.code}`}
                className="block text-[11px] font-semibold uppercase tracking-[0.07em] text-text-muted"
              >
                Notes de suivi
              </label>
              {isSaving && (
                <span className="text-[10.5px] text-text-muted" aria-live="polite">
                  Sauvegarde…
                </span>
              )}
            </div>
            <textarea
              id={`notes-${item.code}`}
              value={localNotes}
              onChange={(e) => handleNotesChange(e.target.value)}
              rows={3}
              placeholder="Obstacles rencontrés, contexte, ressources utiles…"
              className="w-full resize-y rounded-lg border border-border bg-bg px-3 py-2 text-[13px] leading-relaxed text-text outline-none placeholder:text-text-muted focus:border-blue-maat focus:ring-1 focus:ring-blue-maat/20"
            />
          </div>
        </div>
      )}
    </li>
  )
}

const ActionItemCard = memo(ActionItemCardComponent)

// ─── Page ─────────────────────────────────────────────────────────────────────

// docs/specs/recommandations.md, section 4 : outil de travail RSE au quotidien.
// Statut (4 états), responsable, échéance et notes auto-sauvegardées.
export function PlanActionsPage() {
  const dashboardLoadStatus = useDashboardStore((s) => s.loadStatus)
  const hasCompletedDiagnostic = useDashboardStore((s) => s.hasCompletedDiagnostic)
  const latestDiagnosticId = useDashboardStore((s) => s.latestDiagnostic?.id)
  const loadDashboard = useDashboardStore((s) => s.load)

  const [items, setItems] = useState<ActionItemWithProgress[]>([])
  const [loadStatus, setLoadStatus] = useState<'idle' | 'loading' | 'loaded' | 'error'>('idle')
  const [loadError, setLoadError] = useState<string | null>(null)

  const role = useAuthStore((s) => s.user?.role)
  const canEdit = role !== 'Viewer'

  const [effortFilter, setEffortFilter] = useState<Set<EffortLevel>>(() => new Set())
  const [domainFilter, setDomainFilter] = useState<Set<RseDomain>>(() => new Set())
  const [minImpact, setMinImpact] = useState(0)

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

  // Même rechargement inconditionnel que DashboardPage (voir commentaire dans PlanActionsPage
  // précédente) : AppShell ne recharge le store qu'une fois par session.
  useEffect(() => {
    void loadDashboard()
  }, [loadDashboard])

  useEffect(() => {
    if (!latestDiagnosticId) return
    let cancelled = false
    setLoadStatus('loading')
    actionPlanApi.getActionPlan(latestDiagnosticId)
      .then((data) => { if (!cancelled) { setItems(data); setLoadStatus('loaded') } })
      .catch((err: unknown) => {
        if (!cancelled) {
          setLoadError(err instanceof Error ? err.message : "Impossible de récupérer le plan d'actions.")
          setLoadStatus('error')
        }
      })
    return () => { cancelled = true }
  }, [latestDiagnosticId])

  async function handleSave(code: string, payload: UpsertPayload) {
    if (!latestDiagnosticId) return
    const result = await actionPlanApi.upsertActionItemProgress(latestDiagnosticId, code, payload)
    setItems((prev) =>
      prev.map((i) =>
        i.code === code
          ? {
              ...i,
              status: result.status,
              assignedTo: result.assignedTo,
              dueDate: result.dueDate,
              notes: result.notes,
              progressUpdatedAt: result.progressUpdatedAt,
              isCompleted: result.status === 'Done',
            }
          : i
      )
    )
  }

  // ── Early returns ────────────────────────────────────────────────────────────

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
            Vous n&apos;avez pas encore de diagnostic complété. Terminez votre questionnaire RSE pour voir
            apparaître votre plan d&apos;actions.
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
    return <p role="alert" className="text-red">{loadError}</p>
  }

  // ── Données dérivées ─────────────────────────────────────────────────────────

  const completedCount = items.filter((i) => i.status === 'Done').length
  const progressPercent = items.length > 0 ? Math.round((completedCount / items.length) * 100) : 0

  const domainStats = DOMAIN_ORDER
    .map((domain) => {
      const domainItems = items.filter((i) => i.domain === domain)
      if (!domainItems.length) return null
      return {
        domain,
        done: domainItems.filter((i) => i.status === 'Done').length,
        total: domainItems.length,
      }
    })
    .filter(Boolean) as { domain: RseDomain; done: number; total: number }[]

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Plan d'actions" />

      {/* Progression globale + répartition par domaine */}
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

          <div
            className="h-1.5 w-full overflow-hidden rounded-full bg-border"
            role="progressbar"
            aria-valuenow={progressPercent}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-label={`${progressPercent} % des actions terminées`}
          >
            <div
              className="h-full rounded-full bg-green-maat transition-all duration-500"
              style={{ width: `${progressPercent}%` }}
            />
          </div>

          {/* Mini-stats par domaine */}
          {domainStats.length > 1 && (
            <div className="mt-4 grid grid-cols-2 gap-2 sm:grid-cols-3 xl:grid-cols-5">
              {domainStats.map(({ domain, done, total }) => (
                <div key={domain} className="rounded-lg bg-bg px-3 py-2">
                  <p className="truncate text-[10.5px] font-semibold uppercase tracking-[0.07em] text-text-muted">
                    {DOMAIN_LABELS[domain]}
                  </p>
                  <p className="mt-0.5 text-[12.5px] font-semibold tabular-nums text-text">
                    {done}
                    <span className="font-normal text-text-muted"> / {total}</span>
                  </p>
                </div>
              ))}
            </div>
          )}
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
        <Card>
          <p className="text-text-muted">
            Aucune recommandation déclenchée pour ce diagnostic. Bravo : votre démarche RSE est déjà
            mature sur l&apos;ensemble des points évalués.
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
              <ActionItemCard
                key={item.code}
                item={item}
                canEdit={canEdit}
                onSave={(payload) => handleSave(item.code, payload)}
              />
            ))}
          </ul>
        </>
      )}

      {items.length > 0 && (
        <p className="text-xs text-text-muted">
          Cocher une action ne modifie pas le score : elle est prise en compte lors de votre prochain diagnostic.
        </p>
      )}
    </div>
  )
}
