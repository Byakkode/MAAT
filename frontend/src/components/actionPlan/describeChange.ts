import type { ActionItemChange, ActionItemStatus } from '../../api/actionPlanApi'
import { ACTION_STATUS_LABELS } from '../../constants/actionStatusLabels'

const EMPTY = '—'

// Échéance : un jour du calendrier, stocké à minuit UTC — affiché en UTC pour ne jamais
// glisser d'un jour selon le fuseau du navigateur.
const dueDateFormat = new Intl.DateTimeFormat('fr-FR', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' })

function formatValue(field: ActionItemChange['field'], value: string | null): string {
  if (value === null) return EMPTY
  if (field === 'Status') return ACTION_STATUS_LABELS[value as ActionItemStatus] ?? value
  if (field === 'DueDate') return dueDateFormat.format(new Date(`${value}T00:00:00Z`))
  return value
}

// docs/specs/recommandations.md, section 4 bis : une phrase par ligne. Les notes n'ont pas de
// valeur : leur contenu n'est jamais retenu, seulement le fait qu'elles ont changé.
export function describeChange(change: ActionItemChange): string {
  switch (change.field) {
    case 'Notes':
      return 'Notes modifiées'
    case 'Status':
      return `Statut : ${formatValue('Status', change.oldValue)} → ${formatValue('Status', change.newValue)}`
    case 'AssignedTo':
      return `Responsable : ${formatValue('AssignedTo', change.oldValue)} → ${formatValue('AssignedTo', change.newValue)}`
    case 'DueDate':
      return `Échéance : ${formatValue('DueDate', change.oldValue)} → ${formatValue('DueDate', change.newValue)}`
  }
}
