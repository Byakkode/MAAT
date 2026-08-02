import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, renderHook } from '@testing-library/react'

// docs/specs/questionnaire.md, sections 1 (cas 13), 4, 5, 7.
const diagnosticsApi = vi.hoisted(() => ({
  getCurrent: vi.fn(),
  getById: vi.fn(),
  getQuestions: vi.fn(),
  upsertResponse: vi.fn(),
  complete: vi.fn(),
}))
vi.mock('../api/diagnosticsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/diagnosticsApi')>()
  return { ...actual, ...diagnosticsApi }
})

import { useQuestionnaireStore, selectHasSaveError } from './questionnaireStore'
import { IncompleteQuestionnaireError } from '../api/diagnosticsApi'
import { ENV_QUESTIONS, makeDiagnosticDetail, resetQuestionnaireStore, SOCIAL_QUESTIONS } from '../test/questionnaireFixtures'

describe('questionnaireStore', () => {
  beforeEach(() => {
    resetQuestionnaireStore()
    diagnosticsApi.getCurrent.mockReset()
    diagnosticsApi.getById.mockReset()
    diagnosticsApi.getQuestions.mockReset()
    diagnosticsApi.upsertResponse.mockReset()
    diagnosticsApi.complete.mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  describe('load', () => {
    it('regroupe les questions en étapes par domaine, dans l’ordre fixe', async () => {
      diagnosticsApi.getCurrent.mockResolvedValue({
        id: 'diag-1',
        companyId: 'c-1',
        status: 'InProgress',
        createdAt: '2026-01-01T00:00:00Z',
        answeredCount: 0,
        totalActiveQuestions: 3,
      })
      diagnosticsApi.getById.mockResolvedValue(makeDiagnosticDetail('InProgress'))
      diagnosticsApi.getQuestions.mockResolvedValue([...SOCIAL_QUESTIONS, ...ENV_QUESTIONS])

      await useQuestionnaireStore.getState().load()

      const { steps } = useQuestionnaireStore.getState()
      expect(steps.map((s) => s.domain)).toEqual(['Environmental', 'Social'])
      expect(steps[0].questions.map((q) => q.code)).toEqual(['ENV-01', 'ENV-02'])
    })

    it('lit le statut réel via GET /api/diagnostics/{id} (cas 13), jamais déduit de /current', async () => {
      // /current ne retourne rien : si le mode d'affichage était encore déduit d'une
      // comparaison avec /current, ce test échouerait comme avant ce correctif.
      diagnosticsApi.getCurrent.mockResolvedValue(null)
      diagnosticsApi.getById.mockResolvedValue(makeDiagnosticDetail('InProgress', { id: 'diag-1' }))
      diagnosticsApi.getQuestions.mockResolvedValue(ENV_QUESTIONS)

      await useQuestionnaireStore.getState().load('diag-1')

      expect(diagnosticsApi.getById).toHaveBeenCalledWith('diag-1')
      expect(useQuestionnaireStore.getState().isEditable).toBe(true)
      expect(useQuestionnaireStore.getState().diagnosticStatus).toBe('InProgress')
    })

    it('un diagnostic Completed est chargé en lecture seule, avec sa date de complétion', async () => {
      diagnosticsApi.getCurrent.mockResolvedValue(null)
      diagnosticsApi.getById.mockResolvedValue(
        makeDiagnosticDetail('Completed', { id: 'diag-completed', completedAt: '2026-02-01T10:00:00Z' }),
      )
      diagnosticsApi.getQuestions.mockResolvedValue(ENV_QUESTIONS.map((q) => ({ ...q, value: 4 })))

      await useQuestionnaireStore.getState().load('diag-completed')

      expect(useQuestionnaireStore.getState().isEditable).toBe(false)
      expect(useQuestionnaireStore.getState().diagnosticStatus).toBe('Completed')
      expect(useQuestionnaireStore.getState().diagnosticCompletedAt).toBe('2026-02-01T10:00:00Z')
    })

    it('un diagnostic Archived est chargé en lecture seule, sans date de complétion', async () => {
      diagnosticsApi.getCurrent.mockResolvedValue(null)
      diagnosticsApi.getById.mockResolvedValue(makeDiagnosticDetail('Archived', { id: 'diag-archived' }))
      diagnosticsApi.getQuestions.mockResolvedValue(ENV_QUESTIONS)

      await useQuestionnaireStore.getState().load('diag-archived')

      expect(useQuestionnaireStore.getState().isEditable).toBe(false)
      expect(useQuestionnaireStore.getState().diagnosticStatus).toBe('Archived')
      expect(useQuestionnaireStore.getState().diagnosticCompletedAt).toBeNull()
    })

    it('reprend à la première étape comportant une question sans réponse', async () => {
      diagnosticsApi.getCurrent.mockResolvedValue({
        id: 'diag-1',
        companyId: 'c-1',
        status: 'InProgress',
        createdAt: '2026-01-01T00:00:00Z',
        answeredCount: 1,
        totalActiveQuestions: 3,
      })
      diagnosticsApi.getById.mockResolvedValue(makeDiagnosticDetail('InProgress'))
      diagnosticsApi.getQuestions.mockResolvedValue([
        { ...ENV_QUESTIONS[0], value: 5 },
        { ...ENV_QUESTIONS[1], value: 5 },
        SOCIAL_QUESTIONS[0],
      ])

      await useQuestionnaireStore.getState().load()

      expect(useQuestionnaireStore.getState().currentStepIndex).toBe(1)
    })

    it('un diagnostic Completed entièrement répondu est chargé en lecture seule, sans navigation forcée', async () => {
      diagnosticsApi.getCurrent.mockResolvedValue(null)
      diagnosticsApi.getById.mockResolvedValue(makeDiagnosticDetail('Completed', { id: 'diag-completed' }))
      diagnosticsApi.getQuestions.mockResolvedValue(ENV_QUESTIONS.map((q) => ({ ...q, value: 3 })))

      await useQuestionnaireStore.getState().load('diag-completed')

      expect(useQuestionnaireStore.getState().currentStepIndex).toBe(0)
      expect(useQuestionnaireStore.getState().isEditable).toBe(false)
    })
  })

  describe('isolation des sélecteurs (section 7)', () => {
    it('modifier une réponse ne fait rerendre que le composant abonné à cette réponse', async () => {
      diagnosticsApi.getCurrent.mockResolvedValue({
        id: 'diag-1',
        companyId: 'c-1',
        status: 'InProgress',
        createdAt: '2026-01-01T00:00:00Z',
        answeredCount: 0,
        totalActiveQuestions: 2,
      })
      diagnosticsApi.getById.mockResolvedValue(makeDiagnosticDetail('InProgress'))
      diagnosticsApi.getQuestions.mockResolvedValue(ENV_QUESTIONS)
      await useQuestionnaireStore.getState().load()

      let rendersEnv01 = 0
      let rendersEnv02 = 0
      renderHook(() => {
        rendersEnv01 += 1
        return useQuestionnaireStore((s) => s.responses['ENV-01'])
      })
      renderHook(() => {
        rendersEnv02 += 1
        return useQuestionnaireStore((s) => s.responses['ENV-02'])
      })

      expect(rendersEnv01).toBe(1)
      expect(rendersEnv02).toBe(1)

      act(() => {
        useQuestionnaireStore.getState().setAnswer('ENV-01', 4)
      })

      expect(rendersEnv01).toBe(2)
      expect(rendersEnv02).toBe(1)
    })
  })

  describe('sauvegarde automatique (section 4)', () => {
    beforeEach(async () => {
      diagnosticsApi.getCurrent.mockResolvedValue({
        id: 'diag-1',
        companyId: 'c-1',
        status: 'InProgress',
        createdAt: '2026-01-01T00:00:00Z',
        answeredCount: 0,
        totalActiveQuestions: 2,
      })
      diagnosticsApi.getById.mockResolvedValue(makeDiagnosticDetail('InProgress'))
      diagnosticsApi.getQuestions.mockResolvedValue(ENV_QUESTIONS)
      await useQuestionnaireStore.getState().load()
    })

    it('passe par "saving" immédiatement puis "saved" après l’anti-rebond de 500 ms', async () => {
      vi.useFakeTimers()
      diagnosticsApi.upsertResponse.mockResolvedValue({
        id: 'r-1',
        diagnosticId: 'diag-1',
        questionId: 'q-1',
        value: 4,
        answeredAt: '2026-01-01T00:00:00Z',
      })

      act(() => {
        useQuestionnaireStore.getState().setAnswer('ENV-01', 4)
      })

      expect(useQuestionnaireStore.getState().saveStatus['ENV-01']).toBe('saving')
      expect(diagnosticsApi.upsertResponse).not.toHaveBeenCalled()

      await act(async () => {
        await vi.advanceTimersByTimeAsync(500)
      })

      expect(diagnosticsApi.upsertResponse).toHaveBeenCalledWith('diag-1', 'ENV-01', 4)
      expect(useQuestionnaireStore.getState().saveStatus['ENV-01']).toBe('saved')
    })

    it('des sélections successives dans la fenêtre d’anti-rebond n’envoient que la dernière valeur', async () => {
      vi.useFakeTimers()
      diagnosticsApi.upsertResponse.mockResolvedValue({
        id: 'r-1',
        diagnosticId: 'diag-1',
        questionId: 'q-1',
        value: 5,
        answeredAt: '2026-01-01T00:00:00Z',
      })

      act(() => {
        useQuestionnaireStore.getState().setAnswer('ENV-01', 1)
      })
      await act(async () => {
        await vi.advanceTimersByTimeAsync(200)
      })
      act(() => {
        useQuestionnaireStore.getState().setAnswer('ENV-01', 5)
      })
      await act(async () => {
        await vi.advanceTimersByTimeAsync(500)
      })

      expect(diagnosticsApi.upsertResponse).toHaveBeenCalledTimes(1)
      expect(diagnosticsApi.upsertResponse).toHaveBeenCalledWith('diag-1', 'ENV-01', 5)
    })

    it('échec réseau persistant : trois tentatives avec recul exponentiel puis état "error", réponse conservée', async () => {
      vi.useFakeTimers()
      diagnosticsApi.upsertResponse.mockRejectedValue(new Error('Réseau indisponible.'))

      act(() => {
        useQuestionnaireStore.getState().setAnswer('ENV-01', 2)
      })

      await act(async () => {
        await vi.advanceTimersByTimeAsync(500) // anti-rebond -> 1ère tentative
      })
      expect(diagnosticsApi.upsertResponse).toHaveBeenCalledTimes(1)
      expect(useQuestionnaireStore.getState().saveStatus['ENV-01']).toBe('saving')

      await act(async () => {
        await vi.advanceTimersByTimeAsync(500) // 2e tentative
      })
      expect(diagnosticsApi.upsertResponse).toHaveBeenCalledTimes(2)
      expect(useQuestionnaireStore.getState().saveStatus['ENV-01']).toBe('saving')

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000) // 3e tentative
      })
      expect(diagnosticsApi.upsertResponse).toHaveBeenCalledTimes(3)
      expect(useQuestionnaireStore.getState().saveStatus['ENV-01']).toBe('error')
      // La réponse choisie par l'utilisateur reste en mémoire malgré l'échec (section 4).
      expect(useQuestionnaireStore.getState().responses['ENV-01']).toBe(2)
    })

    it('bloque le passage à l’étape suivante tant qu’une sauvegarde est en échec', async () => {
      useQuestionnaireStore.setState({ saveStatus: { 'ENV-01': 'error' } })

      useQuestionnaireStore.getState().nextStep()

      expect(useQuestionnaireStore.getState().currentStepIndex).toBe(0)
    })

    it('n’est pas bloqué quand aucune sauvegarde n’est en échec', async () => {
      diagnosticsApi.getQuestions.mockResolvedValue([...ENV_QUESTIONS, ...SOCIAL_QUESTIONS])
      await useQuestionnaireStore.getState().load()
      useQuestionnaireStore.setState({ saveStatus: { 'ENV-01': 'saved' } })

      useQuestionnaireStore.getState().nextStep()

      expect(useQuestionnaireStore.getState().currentStepIndex).toBe(1)
    })

    it('selectHasSaveError reflète l’état de sauvegarde global', () => {
      useQuestionnaireStore.setState({ saveStatus: { 'ENV-01': 'saved', 'ENV-02': 'error' } })

      expect(selectHasSaveError(useQuestionnaireStore.getState())).toBe(true)
    })
  })

  describe('complétion', () => {
    beforeEach(async () => {
      diagnosticsApi.getCurrent.mockResolvedValue({
        id: 'diag-1',
        companyId: 'c-1',
        status: 'InProgress',
        createdAt: '2026-01-01T00:00:00Z',
        answeredCount: 2,
        totalActiveQuestions: 2,
      })
      diagnosticsApi.getById.mockResolvedValue(makeDiagnosticDetail('InProgress'))
      diagnosticsApi.getQuestions.mockResolvedValue(ENV_QUESTIONS.map((q) => ({ ...q, value: 3 })))
      await useQuestionnaireStore.getState().load()
    })

    it('marque le diagnostic complété et non-éditable en cas de succès, avec le statut renvoyé par le serveur', async () => {
      diagnosticsApi.complete.mockResolvedValue(
        makeDiagnosticDetail('Completed', { completedAt: '2026-01-01T00:20:00Z' }),
      )

      await useQuestionnaireStore.getState().completeDiagnostic()

      expect(useQuestionnaireStore.getState().completeStatus).toBe('completed')
      expect(useQuestionnaireStore.getState().isEditable).toBe(false)
      expect(useQuestionnaireStore.getState().diagnosticStatus).toBe('Completed')
      expect(useQuestionnaireStore.getState().diagnosticCompletedAt).toBe('2026-01-01T00:20:00Z')
    })

    it('conserve les codes de questions manquantes en cas de 400', async () => {
      diagnosticsApi.complete.mockRejectedValue(new IncompleteQuestionnaireError('Incomplet.', ['ENV-02']))

      await expect(useQuestionnaireStore.getState().completeDiagnostic()).rejects.toBeInstanceOf(
        IncompleteQuestionnaireError,
      )

      expect(useQuestionnaireStore.getState().completeStatus).toBe('error')
      expect(useQuestionnaireStore.getState().missingQuestionCodes).toEqual(['ENV-02'])
    })
  })
})
