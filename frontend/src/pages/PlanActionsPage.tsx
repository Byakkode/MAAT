import { ChevronDown, ChevronUp, ClipboardList, SlidersHorizontal, X } from 'lucide-react'
import { type FormEvent, memo, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import * as actionPlanApi from '../api/actionPlanApi'
import * as recommendationsApi from '../api/recommendationsApi'
import { useEntitlements } from '../billing/entitlements'
import { ActionItemHistory } from '../components/actionPlan/ActionItemHistory'
import { ActionStatusMenu } from '../components/actionPlan/ActionStatusMenu'
import { UpgradeNotice } from '../components/billing/UpgradeNotice'
import type { ActionItemStatus, ActionItemWithProgress, UpsertPayload } from '../api/actionPlanApi'
import { EFFORT_LABELS } from '../constants/effortLabels'
import { useAuthStore } from '../store/authStore'
import { useDashboardStore } from '../store/dashboardStore'
import { DOMAIN_LABELS, DOMAIN_ORDER } from '../types/questionnaire'
import type { RseDomain } from '../types/questionnaire'
import type { EffortLevel } from '../types/dashboard'
import { Badge, type BadgeVariant } from '../components/ui/Badge'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { buttonLinkClass } from '../components/ui/buttonStyles'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })
}

function pluralize(count: number, singular: string, plural: string): string {
  return count > 1 ? plural : singular
}

function hiddenActionsTitle(hiddenCount: number): string {
  if (hiddenCount <= 0) return "Suivez l'avancement de vos actions"
  return `${hiddenCount} ${pluralize(hiddenCount, 'autre action recommandée', 'autres actions recommandées')} pour votre entreprise`
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

const FIELD_LABEL = 'mb-1 block text-[11px] font-semibold uppercase tracking-[0.07em] text-text-muted'
const FIELD_INPUT =
  'w-full rounded-lg border border-border bg-bg px-3 py-2 text-[13px] text-text outline-none placeholder:text-text-muted focus:border-blue-maat focus:ring-1 focus:ring-blue-maat/20'

// ─── Carte d'action expandable ────────────────────────────────────────────────

// docs/specs/abonnement.md, section 8, croisé avec le rôle :
// - full : suivi enrichi (statut, responsable, échéance, notes) — Professional ;
// - check : case « terminée » seulement — Essential ;
// - readonly : Starter, ou rôle Viewer quelle que soit l'offre.
// Les détails déjà saisis restent lisibles dans tous les cas (retour d'une offre supérieure).
export type ActionItemMode = 'full' | 'check' | 'readonly'

interface ActionItemCardProps {
  item: ActionItemWithProgress
  mode: ActionItemMode
  diagnosticId: string
  // Professional, tous rôles (recommandations.md, section 4 bis).
  canViewHistory: boolean
  onSave: (payload: UpsertPayload) => Promise<void>
  onToggle: (isCompleted: boolean) => Promise<void>
}

function ActionItemCardComponent({ item, mode, diagnosticId, canViewHistory, onSave, onToggle }: ActionItemCardProps) {
  const canEdit = mode === 'full'
  const [isOpen, setIsOpen] = useState(false)

  // Valeurs enregistrées, et leur copie modifiable dans le formulaire.
  const savedAssignedTo = item.assignedTo ?? ''
  const savedDueDate = item.dueDate ? item.dueDate.slice(0, 10) : ''
  const savedNotes = item.notes ?? ''

  const [localAssignedTo, setLocalAssignedTo] = useState(savedAssignedTo)
  const [localDueDate, setLocalDueDate] = useState(savedDueDate)
  const [localNotes, setLocalNotes] = useState(savedNotes)
  const [isSaving, setIsSaving] = useState(false)
  const [saveState, setSaveState] = useState<'idle' | 'saved' | 'error'>('idle')
  const [statusError, setStatusError] = useState(false)

  // Resynchronise le formulaire quand la valeur enregistrée change (réponse du serveur).
  // Dépend des valeurs, pas de l'objet item : un changement de statut ne doit pas effacer
  // une saisie en cours dans le formulaire.
  useEffect(() => {
    setLocalAssignedTo(savedAssignedTo)
    setLocalDueDate(savedDueDate)
    setLocalNotes(savedNotes)
  }, [savedAssignedTo, savedDueDate, savedNotes])

  // docs/specs/recommandations.md, section 4 bis : chaque enregistrement est un acte volontaire,
  // qui laisse une ligne d'historique par champ réellement modifié.
  // - Statut : choisi dans le menu de l'étiquette, enregistré aussitôt, seul — les autres
  //   champs partent avec leur valeur enregistrée, jamais une saisie en cours.
  // - Responsable, échéance, notes : sur « Enregistrer ». L'enregistrement automatique
  //   écrivait les états intermédiaires : une échéance en « an 2 », « an 20 », « an 200 »
  //   pendant qu'on tapait 2003.
  const isDirty = localAssignedTo.trim() !== savedAssignedTo || localDueDate !== savedDueDate || localNotes !== savedNotes

  async function handleStatusChange(status: ActionItemStatus) {
    setIsSaving(true)
    setStatusError(false)
    try {
      await onSave({ status, assignedTo: item.assignedTo, dueDate: item.dueDate, notes: item.notes })
    } catch {
      setStatusError(true)
    } finally {
      setIsSaving(false)
    }
  }

  async function handleDetailsSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!isDirty) return
    setIsSaving(true)
    try {
      await onSave({
        status: item.status,
        assignedTo: localAssignedTo.trim() || null,
        dueDate: localDueDate || null,
        notes: localNotes || null,
      })
      setSaveState('saved')
    } catch {
      setSaveState('error')
    } finally {
      setIsSaving(false)
    }
  }

  function handleDetailsReset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setLocalAssignedTo(savedAssignedTo)
    setLocalDueDate(savedDueDate)
    setLocalNotes(savedNotes)
    setSaveState('idle')
  }

  // Toute saisie efface le « Enregistré » de l'enregistrement précédent.
  function edit<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value)
      setSaveState('idle')
    }
  }

  async function handleToggle(isCompleted: boolean) {
    setIsSaving(true)
    try {
      await onToggle(isCompleted)
    } finally {
      setIsSaving(false)
    }
  }

  const isDone = mode === 'check' ? item.isCompleted : item.status === 'Done'

  return (
    <li
      className={`rounded-xl border border-border bg-white shadow-card transition-all duration-150 ${
        isDone ? 'opacity-70' : 'hover:border-border-strong hover:shadow-card-hover'
      }`}
    >
      {/* Ligne principale */}
      <div className="flex items-start gap-3 p-4">
        {mode === 'check' ? (
          <input
            type="checkbox"
            checked={item.isCompleted}
            disabled={isSaving}
            onChange={(e) => void handleToggle(e.target.checked)}
            aria-label={`Action terminée : ${item.actionText}`}
            className="mt-1 h-4 w-4 shrink-0 accent-blue-maat"
          />
        ) : (
          <div className="mt-0.5 shrink-0">
            <ActionStatusMenu
              status={item.status}
              canEdit={canEdit}
              disabled={isSaving}
              onChange={(status) => void handleStatusChange(status)}
            />
          </div>
        )}

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

          {statusError && (
            <p role="alert" className="mt-1 text-[11.5px] text-red">
              Le statut n&apos;a pas pu être enregistré. Réessayez.
            </p>
          )}
        </div>

        {/* Bouton d'expansion */}
        {(canEdit || canViewHistory || item.detailText || item.assignedTo || item.dueDate || item.notes) && (
          <button
            type="button"
            onClick={() => setIsOpen(!isOpen)}
            className="shrink-0 rounded-lg border border-border p-1.5 text-text-muted transition-colors hover:border-border-strong hover:text-text"
            aria-expanded={isOpen}
            aria-label={isOpen ? 'Réduire les détails' : canEdit ? 'Modifier les détails' : 'Voir les détails'}
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

          {!canEdit && (
            <dl className="grid grid-cols-1 gap-x-3 gap-y-1 text-[12.5px] sm:grid-cols-[auto_1fr]">
              {item.assignedTo && (
                <>
                  <dt className="font-semibold text-text-muted">Responsable</dt>
                  <dd className="text-text">{item.assignedTo}</dd>
                </>
              )}
              {item.dueDate && (
                <>
                  <dt className="font-semibold text-text-muted">Échéance</dt>
                  <dd className="text-text">{formatDate(item.dueDate)}</dd>
                </>
              )}
              {item.notes && (
                <>
                  <dt className="font-semibold text-text-muted">Notes</dt>
                  <dd className="whitespace-pre-line text-text">{item.notes}</dd>
                </>
              )}
            </dl>
          )}

          {canEdit && (
          <form onSubmit={(e) => void handleDetailsSubmit(e)} onReset={handleDetailsReset}>
          <div className="mb-3 grid grid-cols-1 gap-3 sm:grid-cols-2">
            {/* Responsable */}
            <div>
              <label htmlFor={`assigned-${item.code}`} className={FIELD_LABEL}>
                Responsable
              </label>
              <input
                id={`assigned-${item.code}`}
                type="text"
                value={localAssignedTo}
                onChange={(e) => edit(setLocalAssignedTo)(e.target.value)}
                placeholder="Nom ou poste"
                className={FIELD_INPUT}
              />
            </div>

            {/* Échéance */}
            <div>
              <label htmlFor={`due-${item.code}`} className={FIELD_LABEL}>
                Échéance
              </label>
              <input
                id={`due-${item.code}`}
                type="date"
                value={localDueDate}
                onChange={(e) => edit(setLocalDueDate)(e.target.value)}
                className={FIELD_INPUT}
              />
            </div>
          </div>

          <div>
            <label htmlFor={`notes-${item.code}`} className={FIELD_LABEL}>
              Notes de suivi
            </label>
            <textarea
              id={`notes-${item.code}`}
              value={localNotes}
              onChange={(e) => edit(setLocalNotes)(e.target.value)}
              rows={3}
              placeholder="Obstacles rencontrés, contexte, ressources utiles…"
              className={`${FIELD_INPUT} resize-y leading-relaxed`}
            />
          </div>

          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Button type="submit" size="sm" isLoading={isSaving} disabled={!isDirty || isSaving}>
              Enregistrer
            </Button>
            {isDirty && !isSaving && (
              <Button type="reset" size="sm" variant="ghost">
                Annuler
              </Button>
            )}
            {saveState === 'error' ? (
              <span role="alert" className="text-[11.5px] text-red">
                L&apos;enregistrement a échoué. Réessayez.
              </span>
            ) : (
              <span role="status" className="text-[11.5px] text-text-muted">
                {isDirty ? 'Modifications non enregistrées' : saveState === 'saved' ? 'Enregistré' : ''}
              </span>
            )}
          </div>
          </form>
          )}

          {canViewHistory && (
            <ActionItemHistory diagnosticId={diagnosticId} code={item.code} refreshKey={item.progressUpdatedAt} />
          )}
        </div>
      )}
    </li>
  )
}

const ActionItemCard = memo(ActionItemCardComponent)

// ─── Page ─────────────────────────────────────────────────────────────────────

// docs/specs/recommandations.md, section 4 : outil de travail RSE au quotidien.
// Statut (4 états, menu de l'étiquette), responsable, échéance et notes (« Enregistrer »).
export function PlanActionsPage() {
  const dashboardLoadStatus = useDashboardStore((s) => s.loadStatus)
  const hasCompletedDiagnostic = useDashboardStore((s) => s.hasCompletedDiagnostic)
  const latestDiagnosticId = useDashboardStore((s) => s.latestDiagnostic?.id)
  const triggeredCount = useDashboardStore((s) => s.actionPlan.triggeredCount)
  const loadDashboard = useDashboardStore((s) => s.load)

  const [items, setItems] = useState<ActionItemWithProgress[]>([])
  const [loadStatus, setLoadStatus] = useState<'idle' | 'loading' | 'loaded' | 'error'>('idle')
  const [loadError, setLoadError] = useState<string | null>(null)

  const role = useAuthStore((s) => s.user?.role)
  const entitlements = useEntitlements()
  const mode: ActionItemMode =
    role === 'Viewer' ? 'readonly'
      : entitlements.canEditActionPlan ? 'full'
        : entitlements.canTrackActions ? 'check'
          : 'readonly'

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
              completedAt: result.completedAt,
            }
          : i
      )
    )
  }

  // Essential : la case bascule is_completed seulement ; le statut affiché suit, sans toucher
  // au suivi enrichi (ActionItemProgress) réservé à Professional.
  async function handleToggle(code: string, isCompleted: boolean) {
    if (!latestDiagnosticId) return
    const result = await recommendationsApi.updateRecommendationProgress(latestDiagnosticId, code, isCompleted)
    setItems((prev) =>
      prev.map((i) =>
        i.code === code
          ? {
              ...i,
              isCompleted: result.isCompleted,
              completedAt: result.completedAt,
              status: result.isCompleted ? 'Done' : i.status === 'Done' ? 'Planned' : i.status,
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

      {mode === 'readonly' && role !== 'Viewer' && (
        <UpgradeNotice requiredPlan="Essential" title={hiddenActionsTitle(triggeredCount - items.length)}>
          L&apos;offre Starter présente vos trois actions prioritaires. Essential en montre douze et
          vous permet de cocher celles que vous avez terminées.
        </UpgradeNotice>
      )}

      {mode === 'check' && triggeredCount > items.length && (
        <UpgradeNotice requiredPlan="Professional" title={hiddenActionsTitle(triggeredCount - items.length)}>
          L&apos;offre Essential présente vos douze actions prioritaires. Professional les montre
          toutes, avec un suivi détaillé : statut, responsable, échéance et notes.
        </UpgradeNotice>
      )}

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
                mode={mode}
                diagnosticId={latestDiagnosticId}
                canViewHistory={entitlements.canViewActionHistory}
                onSave={(payload) => handleSave(item.code, payload)}
                onToggle={(isCompleted) => handleToggle(item.code, isCompleted)}
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
