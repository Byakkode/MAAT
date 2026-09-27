import type { ActionItemStatus } from '../api/actionPlanApi'

// Libellés des quatre statuts du plan d'actions, partagés par la carte d'action et son
// historique : un même statut ne doit jamais s'écrire de deux façons à l'écran.
export const ACTION_STATUS_LABELS: Record<ActionItemStatus, string> = {
  Planned: 'Planifié',
  InProgress: 'En cours',
  Blocked: 'Bloqué',
  Done: 'Terminé',
}
