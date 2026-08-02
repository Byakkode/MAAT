import type { RseDomain } from './questionnaire'

// Miroir de MAAT.Api.Controllers.DashboardController.Get() (docs/specs/dashboard.md,
// section 1). Les trois états de la section 1 se lisent sur hasCompletedDiagnostic et
// inProgressDiagnostic : aucun diagnostic (les deux respectivement false/null et null),
// diagnostic en cours seul (hasCompletedDiagnostic=false, inProgressDiagnostic renseigné), au
// moins un complété (contenu complet, inProgressDiagnostic renseigné ou non selon qu'un
// nouveau diagnostic est en cours en parallèle du dernier complété — section 7).
export interface DashboardView {
  hasCompletedDiagnostic: boolean
  latestDiagnostic: LatestDiagnostic | null
  domainScores: DomainScore[]
  history: DiagnosticHistoryPoint[]
  benchmark: SectorBenchmark | null
  actionPlan: ActionPlan
  inProgressDiagnostic: InProgressDiagnostic | null
}

// sectorCode accompagne le score (section 2) : c'est ce qui rend le chiffre explicable.
export interface LatestDiagnostic {
  id: string
  globalScore: number
  completedAt: string
  sectorCode: string
}

// triggeredRecommendationCount porte sur l'intégralité du plan d'actions de ce domaine, pas
// seulement sur ActionPlan.items (au plus cinq) : nécessaire au survol/clic du radar
// (section 3, "le nombre de recommandations qu'il a déclenchées").
export interface DomainScore {
  domain: RseDomain
  score: number
  sectorWeight: number
  triggeredRecommendationCount: number
}

// deltaFromPrevious est nul pour le premier point de l'historique (rien à comparer).
export interface DiagnosticHistoryPoint {
  completedAt: string
  globalScore: number
  deltaFromPrevious: number | null
}

// available=false : reason motive le seuil non atteint, percentile est nul. available=true :
// percentile est renseigné, reason est nul (docs/specs/dashboard.md, section 5).
export interface SectorBenchmark {
  available: boolean
  sampleSize: number
  percentile: number | null
  reason: string | null
}

export type EffortLevel = 'Low' | 'Medium' | 'High'

// Sous-ensemble de MAAT.Application.DTOs.DiagnosticRecommendationView tel que sérialisé par
// DashboardController.Get() : detailText, impactPoints et completedAt n'y figurent pas.
export interface DashboardRecommendation {
  code: string
  actionText: string
  domain: RseDomain
  effortLevel: EffortLevel
  priorityRank: number
  isCompleted: boolean
}

// totalCount et completedCount portent sur l'intégralité du plan, pas seulement sur items (au
// plus cinq, section 6) : « 3 actions terminées sur 24 » doit rester vrai même si les trois
// terminées ne sont pas parmi les cinq affichées.
export interface ActionPlan {
  items: DashboardRecommendation[]
  totalCount: number
  completedCount: number
}

export interface InProgressDiagnostic {
  id: string
  answeredCount: number
  totalActiveQuestions: number
}
