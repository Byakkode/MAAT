import { useQuestionnaireStore } from '../store/questionnaireStore'
import type { DiagnosticDetail, DiagnosticStatus, QuestionAnswer, RseDomain } from '../types/questionnaire'

export function makeQuestion(
  code: string,
  domain: RseDomain,
  displayOrder: number,
  value: number | null = null,
  helpText: string | null = null,
): QuestionAnswer {
  return { code, text: `Texte ${code}`, helpText, domain, displayOrder, value }
}

export const ENV_QUESTIONS: QuestionAnswer[] = [
  makeQuestion('ENV-01', 'Environmental', 1),
  makeQuestion('ENV-02', 'Environmental', 2),
]

export const SOCIAL_QUESTIONS: QuestionAnswer[] = [makeQuestion('SOC-01', 'Social', 1)]

export function makeDiagnosticDetail(status: DiagnosticStatus, overrides: Partial<DiagnosticDetail> = {}): DiagnosticDetail {
  return {
    id: 'diag-1',
    companyId: 'c-1',
    status,
    globalScore: status === 'Completed' ? 3 : null,
    createdAt: '2026-01-01T00:00:00Z',
    completedAt: status === 'Completed' ? '2026-01-01T00:20:00Z' : null,
    ...overrides,
  }
}

// Réinitialise uniquement les champs de données du store, jamais les actions : un
// setState({...}, true) remplacerait tout l'objet d'état, y compris load/setAnswer/etc.
// (create() les définit une seule fois, au même niveau que les champs de données).
export function resetQuestionnaireStore() {
  useQuestionnaireStore.setState({
    loadStatus: 'idle',
    loadError: null,
    diagnosticId: null,
    diagnosticStatus: null,
    diagnosticCompletedAt: null,
    isEditable: false,
    steps: [],
    responses: {},
    saveStatus: {},
    currentStepIndex: 0,
    startedAt: null,
    completeStatus: 'idle',
    completeError: null,
    missingQuestionCodes: [],
  })
}
