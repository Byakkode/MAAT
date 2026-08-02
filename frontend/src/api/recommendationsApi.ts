import { apiFetch } from './httpClient'
import { ApiError } from './authApi'

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  const body = await response.json().catch(() => null)
  return (body as { message?: string } | null)?.message ?? fallback
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
