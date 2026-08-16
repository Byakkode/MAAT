import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { EFFORT_LABELS } from '../constants/effortLabels'
import { useAuthStore } from '../store/authStore'
import { useDashboardStore } from '../store/dashboardStore'
import { usePlanActionsStore } from '../store/planActionsStore'
import { DOMAIN_LABELS } from '../types/questionnaire'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })
}

function pluralize(count: number, singular: string, plural: string): string {
  return count > 1 ? plural : singular
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
      <section className="rounded-card border border-border bg-white p-5 shadow-card">
        <h1 className="mb-2 text-2xl font-semibold text-text">Plan d&apos;actions</h1>
        <p className="mb-3 text-text-muted">
          Vous n&apos;avez pas encore de diagnostic complété. Terminez votre questionnaire RSE pour voir apparaître
          votre plan d&apos;actions.
        </p>
        <Link
          to="/questionnaire"
          className="inline-block rounded-button bg-blue-maat px-4 py-2 font-medium text-white shadow-button"
        >
          Commencer le questionnaire
        </Link>
      </section>
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

  return (
    <section className="rounded-card border border-border bg-white p-5 shadow-card">
      <h1 className="mb-1 text-2xl font-semibold text-text">Plan d&apos;actions</h1>

      {items.length === 0 ? (
        // docs/specs/recommandations.md, section 4 : "Aucune recommandation déclenchée est un
        // résultat valide, pas une erreur [...] laisser le frontend afficher un message de
        // félicitation." Jamais un écran blanc pour ce cas.
        <p className="text-text-muted">
          Aucune recommandation déclenchée pour ce diagnostic. Bravo : votre démarche RSE est déjà mature sur
          l&apos;ensemble des points évalués.
        </p>
      ) : (
        <>
          <p className="mb-3 text-sm text-text-muted tabular-nums lining-nums">
            {completedCount} {pluralize(completedCount, 'action terminée', 'actions terminées')} sur {items.length}
          </p>
          <ul className="flex flex-col gap-4">
            {items.map((item) => (
              <li
                key={item.code}
                className="flex items-start gap-3 border-b border-border pb-4 last:border-b-0 last:pb-0"
              >
                <input
                  type="checkbox"
                  id={`plan-actions-${item.code}`}
                  checked={item.isCompleted}
                  disabled={!canEdit || togglingCode === item.code}
                  onChange={(event) => void toggle(item.code, event.target.checked)}
                  className="mt-1 h-4 w-4 accent-blue-maat"
                />
                <label htmlFor={`plan-actions-${item.code}`} className="flex-1 text-sm text-text">
                  <span className="block font-medium">{item.actionText}</span>
                  <span className="mt-1 block text-text-muted">{item.detailText}</span>
                  <span className="mt-1 block text-text-muted tabular-nums lining-nums">
                    {DOMAIN_LABELS[item.domain]} · {EFFORT_LABELS[item.effortLevel]} · {item.impactPoints} points
                    d&apos;impact
                  </span>
                  {item.isCompleted && item.completedAt && (
                    <span className="mt-1 block text-text-muted tabular-nums lining-nums">
                      Terminée le {formatDate(item.completedAt)}
                    </span>
                  )}
                </label>
              </li>
            ))}
          </ul>
          <p className="mt-4 text-xs text-text-muted">
            Cocher une action ne modifie pas le score : elle est prise en compte lors de votre prochain diagnostic.
          </p>
        </>
      )}
      {toggleError && (
        <p role="alert" className="mt-3 text-sm text-red">
          {toggleError}
        </p>
      )}
    </section>
  )
}
