import { Link } from 'react-router-dom'
import { EFFORT_LABELS } from '../../constants/effortLabels'
import { DOMAIN_LABELS } from '../../types/questionnaire'
import type { ActionPlan, EffortLevel } from '../../types/dashboard'
import { Badge, type BadgeVariant } from '../ui/Badge'
import { Card } from '../ui/Card'

interface ActionPlanCardProps {
  actionPlan: ActionPlan
  // Admin/User : cases actionnables. Viewer : présentes, jamais actionnables (cas 19).
  canEdit: boolean
  togglingCode: string | null
  onToggle: (code: string, isCompleted: boolean) => void
}

function pluralize(count: number, singular: string, plural: string): string {
  return count > 1 ? plural : singular
}

const EFFORT_BADGE_VARIANT: Record<EffortLevel, BadgeVariant> = {
  Low: 'green',
  Medium: 'amber',
  High: 'red',
}

// docs/specs/dashboard.md, section 6.
export function ActionPlanCard({ actionPlan, canEdit, togglingCode, onToggle }: ActionPlanCardProps) {
  const progressPercent =
    actionPlan.totalCount > 0
      ? Math.round((actionPlan.completedCount / actionPlan.totalCount) * 100)
      : 0

  return (
    <Card as="section" aria-labelledby="action-plan-heading">
      <h2 id="action-plan-heading" className="mb-3 text-base font-semibold text-text">
        Plan d&apos;actions
      </h2>

      {/* Barre de progression globale */}
      <div className="mb-4">
        <div className="h-1.5 w-full overflow-hidden rounded-full bg-border">
          <div
            className="h-full rounded-full bg-green-maat transition-all"
            style={{ width: `${progressPercent}%` }}
          />
        </div>
        <p className="mt-1 text-xs text-text-muted">
          {actionPlan.completedCount}{' '}
          {pluralize(actionPlan.completedCount, 'action terminée', 'actions terminées')} sur{' '}
          {actionPlan.totalCount}
        </p>
      </div>

      {actionPlan.items.length === 0 ? (
        <p className="text-sm text-text-muted">Aucune recommandation déclenchée pour ce diagnostic.</p>
      ) : (
        <ul className="flex flex-col divide-y divide-border">
          {actionPlan.items.map((item) => (
            <li key={item.code} className="flex items-start gap-2.5 py-2.5 first:pt-0 last:pb-0">
              <input
                type="checkbox"
                id={`action-plan-${item.code}`}
                checked={item.isCompleted}
                disabled={!canEdit || togglingCode === item.code}
                onChange={(event) => onToggle(item.code, event.target.checked)}
                className="mt-0.5 h-4 w-4 shrink-0 accent-blue-maat"
              />
              <label htmlFor={`action-plan-${item.code}`} className="min-w-0">
                <span className={`block text-sm ${item.isCompleted ? 'text-text-muted line-through' : 'text-text'}`}>
                  {item.actionText}
                </span>
                <span className="mt-1 flex flex-wrap gap-1">
                  <Badge domain={item.domain}>{DOMAIN_LABELS[item.domain]}</Badge>
                  <Badge variant={EFFORT_BADGE_VARIANT[item.effortLevel]}>
                    {EFFORT_LABELS[item.effortLevel]}
                  </Badge>
                </span>
              </label>
            </li>
          ))}
        </ul>
      )}

      <p className="mt-3 text-xs text-text-muted">
        Cocher une action ne modifie pas le score — prise en compte au prochain diagnostic.
      </p>

      <Link to="/plan-actions" className="mt-2 inline-block text-sm font-medium text-blue-maat-text hover:underline">
        Voir tout le plan d&apos;actions →
      </Link>
    </Card>
  )
}
