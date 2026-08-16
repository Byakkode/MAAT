import type { EffortLevel } from '../types/dashboard'

// docs/specs/recommandations.md, section 6 : Low/Medium/High sont les noms de
// MAAT.Domain.Enums.EffortLevel, jamais montrés tels quels — réutilisé par ActionPlanCard (les
// cinq premières lignes du tableau de bord) et PlanActionsPage (la liste complète).
export const EFFORT_LABELS: Record<EffortLevel, string> = {
  Low: 'Effort faible',
  Medium: 'Effort modéré',
  High: 'Effort important',
}
