import { usePlanActionsStore } from '../store/planActionsStore'
import type { RecommendationDetail } from '../types/recommendations'
import type { ActionItemWithProgress } from '../api/actionPlanApi'

export function makeRecommendationDetail(overrides: Partial<RecommendationDetail> = {}): RecommendationDetail {
  return {
    code: 'REC-ENV-01',
    actionText: 'Mettre en place un suivi mensuel de vos émissions.',
    detailText: 'Un tableau de suivi simple suffit pour commencer.',
    domain: 'Environmental',
    effortLevel: 'Medium',
    impactPoints: 5,
    priorityRank: 1,
    isCompleted: false,
    completedAt: null,
    ...overrides,
  }
}

export function makeActionItemWithProgress(overrides: Partial<ActionItemWithProgress> = {}): ActionItemWithProgress {
  return {
    code: 'REC-ENV-01',
    actionText: 'Mettre en place un suivi mensuel de vos émissions.',
    detailText: 'Un tableau de suivi simple suffit pour commencer.',
    domain: 'Environmental',
    effortLevel: 'Medium',
    impactPoints: 5,
    priorityRank: 1,
    isCompleted: false,
    completedAt: null,
    status: 'Planned',
    assignedTo: null,
    dueDate: null,
    notes: null,
    progressUpdatedAt: null,
    ...overrides,
  }
}

// Même précaution que resetDashboardStore (voir dashboardFixtures.ts) : ne réinitialise que
// les champs de données, jamais les actions.
export function resetPlanActionsStore() {
  usePlanActionsStore.setState({
    loadStatus: 'idle',
    loadError: null,
    diagnosticId: null,
    items: [],
    togglingCode: null,
    toggleError: null,
  })
}
