import { apiFetch } from './httpClient'
import { ApiError } from './authApi'
import type { DiagnosticDetail, DiagnosticSummary, QuestionAnswer } from '../types/questionnaire'

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  const body = await response.json().catch(() => null)
  return (body as { message?: string } | null)?.message ?? fallback
}

// docs/specs/questionnaire.md, section 5 : le diagnostic InProgress de l'entreprise courante,
// ou null quand il n'y en a aucun (404) — ce n'est pas une erreur applicative. Sert
// uniquement à résoudre quel diagnostic ouvrir quand aucun identifiant n'est fourni ; ne
// jamais s'en servir pour déterminer si un diagnostic donné est modifiable, seul son statut
// (lu via getById) fait foi.
export async function getCurrent(): Promise<DiagnosticSummary | null> {
  const response = await apiFetch('/api/diagnostics/current')
  if (response.status === 404) {
    return null
  }
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de récupérer le diagnostic en cours.'), response.status)
  }
  return (await response.json()) as DiagnosticSummary
}

// section 1, cas 13 : source de vérité du statut réel d'un diagnostic (InProgress, Completed
// ou Archived). C'est le seul endroit d'où le frontend doit lire si un diagnostic est
// modifiable — jamais déduit indirectement (ex. comparaison avec /current).
export async function getById(diagnosticId: string): Promise<DiagnosticDetail> {
  const response = await apiFetch(`/api/diagnostics/${diagnosticId}`)
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Diagnostic introuvable.'), response.status)
  }
  return (await response.json()) as DiagnosticDetail
}

// section 2 : les questions actives triées par domaine puis display_order, avec la réponse
// déjà enregistrée le cas échéant. Fonctionne aussi sur un diagnostic Completed (lecture seule).
export async function getQuestions(diagnosticId: string): Promise<QuestionAnswer[]> {
  const response = await apiFetch(`/api/diagnostics/${diagnosticId}/questions`)
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de récupérer les questions.'), response.status)
  }
  return (await response.json()) as QuestionAnswer[]
}

export interface UpsertResponseResult {
  id: string
  diagnosticId: string
  questionId: string
  value: number
  answeredAt: string
}

// section 4 : upsert idempotent sur (diagnostic_id, question_id).
export async function upsertResponse(
  diagnosticId: string,
  questionCode: string,
  value: number,
): Promise<UpsertResponseResult> {
  const response = await apiFetch(`/api/diagnostics/${diagnosticId}/responses/${questionCode}`, {
    method: 'PUT',
    body: JSON.stringify({ value }),
  })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, "Échec de l'enregistrement de la réponse."), response.status)
  }
  return (await response.json()) as UpsertResponseResult
}

// section 6, cas 10 : distingue le 400 « questions manquantes » des autres erreurs, pour que
// l'appelant puisse afficher la liste des codes concernés plutôt qu'un message générique.
export class IncompleteQuestionnaireError extends ApiError {
  readonly missingQuestionCodes: string[]

  constructor(message: string, missingQuestionCodes: string[]) {
    super(message, 400)
    this.missingQuestionCodes = missingQuestionCodes
  }
}

export async function complete(diagnosticId: string): Promise<DiagnosticDetail> {
  const response = await apiFetch(`/api/diagnostics/${diagnosticId}/complete`, { method: 'POST' })
  if (response.status === 400) {
    const body = (await response.json().catch(() => null)) as { message?: string; missingQuestionCodes?: string[] } | null
    throw new IncompleteQuestionnaireError(body?.message ?? 'Questionnaire incomplet.', body?.missingQuestionCodes ?? [])
  }
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de finaliser le diagnostic.'), response.status)
  }
  return (await response.json()) as DiagnosticDetail
}
