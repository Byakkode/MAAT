import { ClipboardList } from 'lucide-react'
import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { EFFORT_LABELS } from '../constants/effortLabels'
import { useAuthStore } from '../store/authStore'
import { useDashboardStore } from '../store/dashboardStore'
import { usePlanActionsStore } from '../store/planActionsStore'
import { DOMAIN_LABELS } from '../types/questionnaire'
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

// Couleurs par domaine RSE — identiques aux tokens chart-* de index.css pour cohérence visuelle.
const DOMAIN_BORDER_COLORS: Record<string, string> = {
  Environmental: '#29CC6A',
  Social:        '#1E88E5',
  Ethics:        '#7E57C2',
  Procurement:   '#FFB74D',
  Governance:    '#42A5F5',
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

  // section 5, cas 21 : cases actionnables pour Admin et User, lecture seule pour Viewer.
  const role = useAuthStore((s) => s.user?.role)
  const canEdit = role !== 'Viewer'

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

  // AppShell déclenche déjà useDashboardStore.load() une fois par session (badge d'avancement
  // de la barre latérale) : cet écran attend ce chargement plutôt que d'en lancer un second.
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

  // Un compte sans diagnostic complété n'a pas de plan d'actions à afficher — une invitation à
  // en terminer un, jamais un écran vide silencieux.
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

  const completedCount = items.filter((item) => item.isCompleted).length
  const progressPercent = items.length > 0 ? Math.round((completedCount / items.length) * 100) : 0

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Plan d'actions" />

      {/* En-tête : compteur et barre de progression */}
      {items.length > 0 && (
        <Card>
          <div className="mb-3 grid grid-cols-[1fr_auto_1fr] items-center">
            <p className="text-sm text-text-muted tabular-nums lining-nums">
              {completedCount}{' '}
              {pluralize(completedCount, 'action terminée', 'actions terminées')} sur {items.length}
            </p>
            <strong className="px-4 text-2xl font-bold tabular-nums text-green-maat" aria-hidden="true">
              {progressPercent}&nbsp;%
            </strong>
            <p className="text-right text-sm text-text-muted tabular-nums">
              {items.length - completedCount}{' '}
              {pluralize(items.length - completedCount, 'action restante', 'actions restantes')}
            </p>
          </div>
          <div className="h-2.5 w-full overflow-hidden rounded-full bg-border">
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
      ) : (
        <ul className="flex flex-col gap-3">
          {items.map((item) => (
            <li
              key={item.code}
              className={`rounded-card border border-border border-l-4 bg-white p-4 shadow-card transition-opacity ${
                item.isCompleted ? 'opacity-60' : ''
              }`}
              style={{ borderLeftColor: DOMAIN_BORDER_COLORS[item.domain] ?? '#E5E7EB' }}
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
                <label htmlFor={`plan-actions-${item.code}`} className="flex-1 cursor-pointer space-y-1">
                  <span
                    className={`block text-sm font-medium ${
                      item.isCompleted ? 'text-text-muted line-through' : 'text-text'
                    }`}
                  >
                    {item.actionText}
                  </span>

                  <span className="block text-xs text-text-muted">{item.detailText}</span>

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
                    <span className="block text-xs font-medium text-green-maat-text tabular-nums lining-nums">
                      Terminée le {formatDate(item.completedAt)}
                    </span>
                  )}
                </label>
              </div>
            </li>
          ))}
        </ul>
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
