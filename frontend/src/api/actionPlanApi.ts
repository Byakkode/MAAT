import { apiFetch } from './httpClient'
import { ApiError } from './authApi'
import type { RseDomain } from '../types/questionnaire'
import type { EffortLevel } from '../types/dashboard'

export type ActionItemStatus = 'Planned' | 'InProgress' | 'Blocked' | 'Done'

// Recommandation déclenchée enrichie du suivi de progression (notes, responsable, échéance).
export interface ActionItemWithProgress {
  code: string
  actionText: string
  detailText: string | null
  domain: RseDomain
  effortLevel: EffortLevel
  impactPoints: number
  priorityRank: number
  isCompleted: boolean
  completedAt: string | null
  status: ActionItemStatus
  assignedTo: string | null
  dueDate: string | null
  notes: string | null
  progressUpdatedAt: string | null
}

export interface UpsertPayload {
  status: ActionItemStatus
  assignedTo: string | null
  dueDate: string | null
  notes: string | null
}

export interface UpsertResult {
  diagnosticId: string
  code: string
  status: ActionItemStatus
  assignedTo: string | null
  dueDate: string | null
  notes: string | null
  progressUpdatedAt: string
  completedAt: string | null
}

async function readError(res: Response, fallback: string): Promise<string> {
  const body = await res.json().catch(() => null)
  return (body as { message?: string } | null)?.message ?? fallback
}

export async function getActionPlan(diagnosticId: string): Promise<ActionItemWithProgress[]> {
  const res = await apiFetch(`/api/diagnostics/${diagnosticId}/action-plan`)
  if (!res.ok) {
    throw new ApiError(await readError(res, "Impossible de récupérer le plan d'actions."), res.status)
  }
  return (await res.json()) as ActionItemWithProgress[]
}

export async function upsertActionItemProgress(
  diagnosticId: string,
  code: string,
  payload: UpsertPayload,
): Promise<UpsertResult> {
  const res = await apiFetch(`/api/diagnostics/${diagnosticId}/action-plan/${code}`, {
    method: 'PATCH',
    body: JSON.stringify(payload),
  })
  if (!res.ok) {
    throw new ApiError(await readError(res, "Échec de la mise à jour de l'action."), res.status)
  }
  return (await res.json()) as UpsertResult
}

// docs/specs/recommandations.md, section 4 bis. Valeurs brutes : nom de statut, texte du
// responsable, date aaaa-mm-jj ; toujours null pour les notes, dont le contenu n'est pas
// retenu. changedBy : null quand le compte de l'auteur a été supprimé.
export type ActionItemField = 'Status' | 'AssignedTo' | 'DueDate' | 'Notes'

export interface ActionItemChange {
  field: ActionItemField
  oldValue: string | null
  newValue: string | null
  changedAt: string
  changedBy: string | null
}

export async function getActionItemHistory(diagnosticId: string, code: string): Promise<ActionItemChange[]> {
  const res = await apiFetch(`/api/diagnostics/${diagnosticId}/action-plan/${code}/history`)
  if (!res.ok) {
    throw new ApiError(await readError(res, "Impossible de récupérer l'historique de l'action."), res.status)
  }
  return (await res.json()) as ActionItemChange[]
}
