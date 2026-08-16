import { apiFetch } from './httpClient'
import { ApiError } from './authApi'
import type { RecommendationDetail } from '../types/recommendations'

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  const body = await response.json().catch(() => null)
  return (body as { message?: string } | null)?.message ?? fallback
}

// docs/specs/recommandations.md, section 4 : liste complète, triée par priority_rank par le
// serveur, accessible aux trois rôles (Viewer compris — c'est une lecture). Un diagnostic
// d'une autre entreprise répond 404, traduit ici en ApiError comme les autres endpoints
// scopés par entreprise.
export async function getRecommendations(diagnosticId: string): Promise<RecommendationDetail[]> {
  const response = await apiFetch(`/api/diagnostics/${diagnosticId}/recommendations`)
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de récupérer le plan d’actions.'), response.status)
  }
  return (await response.json()) as RecommendationDetail[]
}

export interface UpdateRecommendationProgressResult {
  diagnosticId: string
  recommendationId: string
  priorityRank: number
  isCompleted: boolean
  completedAt: string | null
}

// docs/specs/dashboard.md, section 6 : bascule d'une case du plan d'actions. Réservé à Admin
// et User côté backend ([Authorize(Roles = "Admin,User")]) — un Viewer reçoit un 403, jamais
// exposé à cette fonction puisque ses cases ne sont pas actionnables (cas 19).
export async function updateRecommendationProgress(
  diagnosticId: string,
  recommendationCode: string,
  isCompleted: boolean,
): Promise<UpdateRecommendationProgressResult> {
  const response = await apiFetch(`/api/diagnostics/${diagnosticId}/recommendations/${recommendationCode}`, {
    method: 'PATCH',
    body: JSON.stringify({ isCompleted }),
  })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, "Échec de la mise à jour de l'action."), response.status)
  }
  return (await response.json()) as UpdateRecommendationProgressResult
}
