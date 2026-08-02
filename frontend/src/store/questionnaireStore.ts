import { create } from 'zustand'
import * as diagnosticsApi from '../api/diagnosticsApi'
import { IncompleteQuestionnaireError } from '../api/diagnosticsApi'
import { DOMAIN_ORDER } from '../types/questionnaire'
import type { DiagnosticStatus, QuestionAnswer, RseDomain } from '../types/questionnaire'

export type LoadStatus = 'idle' | 'loading' | 'loaded' | 'error'
export type SaveStatus = 'idle' | 'saving' | 'saved' | 'error'
export type CompleteStatus = 'idle' | 'completing' | 'completed' | 'error'

export interface QuestionnaireStep {
  domain: RseDomain
  questions: QuestionAnswer[]
}

interface QuestionnaireState {
  loadStatus: LoadStatus
  loadError: string | null
  diagnosticId: string | null
  // section 1, cas 13 : lu depuis GET /api/diagnostics/{id} (diagnosticsApi.getById), jamais
  // déduit indirectement. isEditable découle uniquement de diagnosticStatus === 'InProgress'.
  diagnosticStatus: DiagnosticStatus | null
  diagnosticCompletedAt: string | null
  isEditable: boolean
  steps: QuestionnaireStep[]
  responses: Record<string, number | null>
  saveStatus: Record<string, SaveStatus>
  currentStepIndex: number
  startedAt: number | null
  completeStatus: CompleteStatus
  completeError: string | null
  missingQuestionCodes: string[]

  load: (explicitDiagnosticId?: string) => Promise<void>
  setAnswer: (code: string, value: number) => void
  nextStep: () => void
  prevStep: () => void
  goToStep: (index: number) => void
  retryFailedSaves: () => void
  completeDiagnostic: () => Promise<void>
}

const DEBOUNCE_MS = 500
// section 4 : trois tentatives avec recul exponentiel — deux délais d'attente entre les trois
// essais (immédiat, +500 ms, +1000 ms).
const RETRY_DELAYS_MS = [500, 1000]

const debounceTimers = new Map<string, ReturnType<typeof setTimeout>>()

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

function groupIntoSteps(questions: QuestionAnswer[]): QuestionnaireStep[] {
  return DOMAIN_ORDER.map((domain) => ({
    domain,
    questions: questions.filter((q) => q.domain === domain),
  })).filter((step) => step.questions.length > 0)
}

function findFirstIncompleteStepIndex(steps: QuestionnaireStep[], responses: Record<string, number | null>): number {
  return steps.findIndex((step) => step.questions.some((q) => responses[q.code] === null))
}

const initialState: Omit<
  QuestionnaireState,
  'load' | 'setAnswer' | 'nextStep' | 'prevStep' | 'goToStep' | 'retryFailedSaves' | 'completeDiagnostic'
> = {
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
}

export const useQuestionnaireStore = create<QuestionnaireState>((set, get) => {
  async function persistWithRetry(diagnosticId: string, code: string, value: number, attempt = 0): Promise<void> {
    try {
      await diagnosticsApi.upsertResponse(diagnosticId, code, value)
      // La réponse a pu changer pendant que cet appel était en vol (nouvelle sélection de
      // l'utilisateur) : un cycle de sauvegarde plus récent est alors déjà planifié pour ce
      // code, et ce succès obsolète ne doit pas écraser son statut.
      if (get().responses[code] !== value) {
        return
      }
      set((state) => ({ saveStatus: { ...state.saveStatus, [code]: 'saved' } }))
    } catch {
      if (attempt < RETRY_DELAYS_MS.length) {
        await delay(RETRY_DELAYS_MS[attempt])
        return persistWithRetry(diagnosticId, code, value, attempt + 1)
      }
      set((state) => ({ saveStatus: { ...state.saveStatus, [code]: 'error' } }))
    }
  }

  function scheduleSave(diagnosticId: string, code: string, value: number) {
    const existingTimer = debounceTimers.get(code)
    if (existingTimer) {
      clearTimeout(existingTimer)
    }
    const timer = setTimeout(() => {
      debounceTimers.delete(code)
      void persistWithRetry(diagnosticId, code, value)
    }, DEBOUNCE_MS)
    debounceTimers.set(code, timer)
  }

  return {
    ...initialState,

    async load(explicitDiagnosticId) {
      set({ ...initialState, loadStatus: 'loading' })

      // /current sert uniquement à résoudre quel diagnostic ouvrir quand aucun identifiant
      // n'est fourni (reprise depuis le tableau de bord, section 5) — jamais à décider s'il
      // est modifiable, ce que seul getById (cas 13) peut établir.
      let targetId = explicitDiagnosticId
      if (!targetId) {
        try {
          const current = await diagnosticsApi.getCurrent()
          targetId = current?.id
        } catch {
          targetId = undefined
        }
      }

      if (!targetId) {
        set({ loadStatus: 'error', loadError: 'Aucun diagnostic à afficher.' })
        return
      }

      try {
        const [detail, questions] = await Promise.all([
          diagnosticsApi.getById(targetId),
          diagnosticsApi.getQuestions(targetId),
        ])
        const steps = groupIntoSteps(questions)
        const responses = Object.fromEntries(questions.map((q) => [q.code, q.value]))
        const isEditable = detail.status === 'InProgress'
        const firstIncomplete = findFirstIncompleteStepIndex(steps, responses)

        set({
          diagnosticId: targetId,
          diagnosticStatus: detail.status,
          diagnosticCompletedAt: detail.completedAt,
          isEditable,
          steps,
          responses,
          saveStatus: {},
          currentStepIndex: isEditable ? (firstIncomplete === -1 ? Math.max(steps.length - 1, 0) : firstIncomplete) : 0,
          startedAt: Date.now(),
          loadStatus: 'loaded',
        })
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Impossible de charger le questionnaire.'
        set({ loadStatus: 'error', loadError: message })
      }
    },

    setAnswer(code, value) {
      const { diagnosticId, isEditable } = get()
      if (!diagnosticId || !isEditable) {
        return
      }
      set((state) => ({
        responses: { ...state.responses, [code]: value },
        saveStatus: { ...state.saveStatus, [code]: 'saving' },
      }))
      scheduleSave(diagnosticId, code, value)
    },

    // section 4 : bloqué tant qu'une sauvegarde est en échec.
    nextStep() {
      const { currentStepIndex, steps, saveStatus } = get()
      if (Object.values(saveStatus).some((status) => status === 'error')) {
        return
      }
      if (currentStepIndex < steps.length - 1) {
        set({ currentStepIndex: currentStepIndex + 1 })
      }
    },

    prevStep() {
      const { currentStepIndex } = get()
      if (currentStepIndex > 0) {
        set({ currentStepIndex: currentStepIndex - 1 })
      }
    },

    goToStep(index) {
      const { steps } = get()
      if (index >= 0 && index < steps.length) {
        set({ currentStepIndex: index })
      }
    },

    retryFailedSaves() {
      const { diagnosticId, saveStatus, responses } = get()
      if (!diagnosticId) {
        return
      }
      Object.entries(saveStatus)
        .filter(([, status]) => status === 'error')
        .forEach(([code]) => {
          const value = responses[code]
          if (value === null || value === undefined) {
            return
          }
          set((state) => ({ saveStatus: { ...state.saveStatus, [code]: 'saving' } }))
          void persistWithRetry(diagnosticId, code, value)
        })
    },

    async completeDiagnostic() {
      const { diagnosticId } = get()
      if (!diagnosticId) {
        return
      }
      set({ completeStatus: 'completing', completeError: null, missingQuestionCodes: [] })
      try {
        const detail = await diagnosticsApi.complete(diagnosticId)
        set({
          completeStatus: 'completed',
          isEditable: false,
          diagnosticStatus: detail.status,
          diagnosticCompletedAt: detail.completedAt,
        })
      } catch (err) {
        if (err instanceof IncompleteQuestionnaireError) {
          set({ completeStatus: 'error', completeError: err.message, missingQuestionCodes: err.missingQuestionCodes })
        } else {
          const message = err instanceof Error ? err.message : 'Impossible de finaliser le diagnostic.'
          set({ completeStatus: 'error', completeError: message })
        }
        throw err
      }
    },
  }
})

export function selectHasSaveError(state: Pick<QuestionnaireState, 'saveStatus'>): boolean {
  return Object.values(state.saveStatus).some((status) => status === 'error')
}

export function selectAnsweredCount(state: Pick<QuestionnaireState, 'responses'>): number {
  return Object.values(state.responses).filter((value) => value !== null).length
}

export function selectTotalQuestions(state: Pick<QuestionnaireState, 'steps'>): number {
  return state.steps.reduce((total, step) => total + step.questions.length, 0)
}
