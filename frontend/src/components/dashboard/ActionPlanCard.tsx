import { Link } from 'react-router-dom'
import { DOMAIN_LABELS } from '../../types/questionnaire'
import type { ActionPlan, EffortLevel } from '../../types/dashboard'

interface ActionPlanCardProps {
  actionPlan: ActionPlan
  // Admin/User : cases actionnables. Viewer : présentes, jamais actionnables (cas 19).
  canEdit: boolean
  togglingCode: string | null
  onToggle: (code: string, isCompleted: boolean) => void
}

const EFFORT_LABELS: Record<EffortLevel, string> = {
  Low: 'Effort faible',
  Medium: 'Effort modéré',
  High: 'Effort important',
}

function pluralize(count: number, singular: string, plural: string): string {
  return count > 1 ? plural : singular
}

// docs/specs/dashboard.md, section 6.
export function ActionPlanCard({ actionPlan, canEdit, togglingCode, onToggle }: ActionPlanCardProps) {
  return (
    <section aria-labelledby="action-plan-heading" className="rounded-card border border-border bg-white p-5 shadow-card">
      <h3 id="action-plan-heading" className="mb-1 text-base font-semibold text-text">
        Plan d&apos;actions
      </h3>
      <p className="mb-3 text-sm text-text-muted tabular-nums lining-nums">
        {actionPlan.completedCount} {pluralize(actionPlan.completedCount, 'action terminée', 'actions terminées')} sur{' '}
        {actionPlan.totalCount}
      </p>

      {actionPlan.items.length === 0 ? (
        <p className="text-sm text-text-muted">Aucune recommandation déclenchée pour ce diagnostic.</p>
      ) : (
        <ul className="flex flex-col gap-3">
          {actionPlan.items.map((item) => (
            <li key={item.code} className="flex items-start gap-2">
              <input
                type="checkbox"
                id={`action-plan-${item.code}`}
                checked={item.isCompleted}
                disabled={!canEdit || togglingCode === item.code}
                onChange={(event) => onToggle(item.code, event.target.checked)}
                className="mt-1 h-4 w-4 accent-blue-maat"
              />
              <label htmlFor={`action-plan-${item.code}`} className="text-sm text-text">
                <span className="block">{item.actionText}</span>
                <span className="text-text-muted">
                  {DOMAIN_LABELS[item.domain]} · {EFFORT_LABELS[item.effortLevel]}
                </span>
              </label>
            </li>
          ))}
        </ul>
      )}

      <p className="mt-3 text-xs text-text-muted">
        Cocher une action ne modifie pas le score : elle est prise en compte lors de votre prochain diagnostic.
      </p>

      <Link to="/plan-actions" className="mt-2 inline-block text-sm font-medium text-blue-maat-text">
        Voir tout le plan d&apos;actions
      </Link>
    </section>
  )
}
