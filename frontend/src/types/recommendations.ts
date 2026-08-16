import type { RseDomain } from './questionnaire'
import type { EffortLevel } from './dashboard'

// Miroir de MAAT.Api.Controllers.DiagnosticsController.GetRecommendations()
// (docs/specs/recommandations.md, section 4) : la liste complète d'un diagnostic, triée par
// priority_rank par le serveur. Contrairement à DashboardRecommendation (dashboard.md, section
// 6, au plus cinq lignes), porte aussi detailText, impactPoints et completedAt.
export interface RecommendationDetail {
  code: string
  actionText: string
  detailText: string
  domain: RseDomain
  effortLevel: EffortLevel
  impactPoints: number
  priorityRank: number
  isCompleted: boolean
  completedAt: string | null
}
