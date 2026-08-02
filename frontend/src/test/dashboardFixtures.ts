import { useDashboardStore } from '../store/dashboardStore'
import { DOMAIN_ORDER } from '../types/questionnaire'
import type { RseDomain } from '../types/questionnaire'
import type {
  ActionPlan,
  DashboardRecommendation,
  DashboardView,
  DiagnosticHistoryPoint,
  DomainScore,
  InProgressDiagnostic,
  LatestDiagnostic,
  SectorBenchmark,
} from '../types/dashboard'

export function makeDomainScore(
  domain: RseDomain,
  score: number,
  sectorWeight = 0.2,
  triggeredRecommendationCount = 0,
): DomainScore {
  return { domain, score, sectorWeight, triggeredRecommendationCount }
}

export function makeAllDomainScores(score = 60): DomainScore[] {
  return DOMAIN_ORDER.map((domain) => makeDomainScore(domain, score))
}

export function makeLatestDiagnostic(overrides: Partial<LatestDiagnostic> = {}): LatestDiagnostic {
  return {
    id: 'diag-1',
    globalScore: 60,
    completedAt: '2026-05-18T14:22:00Z',
    sectorCode: '6201Z',
    ...overrides,
  }
}

export function makeHistoryPoint(overrides: Partial<DiagnosticHistoryPoint> = {}): DiagnosticHistoryPoint {
  return { completedAt: '2026-05-18T14:22:00Z', globalScore: 60, deltaFromPrevious: null, ...overrides }
}

export function makeRecommendation(overrides: Partial<DashboardRecommendation> = {}): DashboardRecommendation {
  return {
    code: 'REC-ENV-01',
    actionText: 'Mettre en place un suivi mensuel de vos émissions.',
    domain: 'Environmental',
    effortLevel: 'Medium',
    priorityRank: 1,
    isCompleted: false,
    ...overrides,
  }
}

export function makeActionPlan(overrides: Partial<ActionPlan> = {}): ActionPlan {
  return { items: [], totalCount: 0, completedCount: 0, ...overrides }
}

export function makeBenchmark(overrides: Partial<SectorBenchmark> = {}): SectorBenchmark {
  return { available: true, sampleSize: 5, percentile: 67, reason: null, ...overrides }
}

export function makeInProgressDiagnostic(overrides: Partial<InProgressDiagnostic> = {}): InProgressDiagnostic {
  return { id: 'diag-progress', answeredCount: 12, totalActiveQuestions: 45, ...overrides }
}

export function makeDashboardView(overrides: Partial<DashboardView> = {}): DashboardView {
  return {
    hasCompletedDiagnostic: false,
    latestDiagnostic: null,
    domainScores: [],
    history: [],
    benchmark: null,
    actionPlan: makeActionPlan(),
    inProgressDiagnostic: null,
    ...overrides,
  }
}

// Réinitialise uniquement les champs de données du store, jamais les actions — même
// précaution que resetQuestionnaireStore (voir questionnaireFixtures.ts).
export function resetDashboardStore() {
  useDashboardStore.setState({
    loadStatus: 'idle',
    loadError: null,
    togglingCode: null,
    toggleError: null,
    ...makeDashboardView(),
  })
}
