import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

// docs/specs/questionnaire.md. Contrairement à authApi.ts (fetch brut, les endpoints
// /api/auth/* n'exigent pas de token), les endpoints /api/diagnostics/* sont [Authorize] :
// diagnosticsApi.ts passe donc par apiFetch (httpClient.ts), seul point d'entrée qui attache
// le token en mémoire et rejoue sur 401 après refresh.
const httpClient = vi.hoisted(() => ({ apiFetch: vi.fn() }))
vi.mock('./httpClient', () => httpClient)

import {
  complete,
  getById,
  getCurrent,
  getQuestions,
  IncompleteQuestionnaireError,
  upsertResponse,
} from './diagnosticsApi'
import { ApiError } from './authApi'

describe('diagnosticsApi', () => {
  beforeEach(() => {
    httpClient.apiFetch.mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('getCurrent', () => {
    it('retourne le diagnostic en cours', async () => {
      httpClient.apiFetch.mockResolvedValue(
        new Response(
          JSON.stringify({
            id: 'diag-1',
            companyId: 'company-1',
            status: 'InProgress',
            createdAt: '2026-01-01T00:00:00Z',
            answeredCount: 2,
            totalActiveQuestions: 3,
          }),
          { status: 200 },
        ),
      )

      const result = await getCurrent()

      expect(result?.id).toBe('diag-1')
      expect(result?.answeredCount).toBe(2)
      expect(httpClient.apiFetch).toHaveBeenCalledWith('/api/diagnostics/current')
    })

    it('retourne null sans lever quand aucun diagnostic n’est en cours (404)', async () => {
      httpClient.apiFetch.mockResolvedValue(new Response('{}', { status: 404 }))

      await expect(getCurrent()).resolves.toBeNull()
    })

    it('lève une ApiError sur une erreur serveur', async () => {
      httpClient.apiFetch.mockResolvedValue(new Response(JSON.stringify({ message: 'Erreur.' }), { status: 500 }))

      await expect(getCurrent()).rejects.toBeInstanceOf(ApiError)
    })
  })

  describe('getById', () => {
    it('retourne le statut réel du diagnostic, seule source fiable pour décider s’il est modifiable', async () => {
      httpClient.apiFetch.mockResolvedValue(
        new Response(
          JSON.stringify({
            id: 'diag-1',
            companyId: 'company-1',
            status: 'Archived',
            globalScore: null,
            createdAt: '2026-01-01T00:00:00Z',
            completedAt: null,
          }),
          { status: 200 },
        ),
      )

      const result = await getById('diag-1')

      expect(result.status).toBe('Archived')
      expect(httpClient.apiFetch).toHaveBeenCalledWith('/api/diagnostics/diag-1')
    })

    it('lève une ApiError sur un diagnostic introuvable ou d’une autre entreprise (404)', async () => {
      httpClient.apiFetch.mockResolvedValue(new Response(null, { status: 404 }))

      await expect(getById('diag-inconnu')).rejects.toBeInstanceOf(ApiError)
    })
  })

  describe('getQuestions', () => {
    it('récupère les questions d’un diagnostic', async () => {
      const questions = [
        { code: 'ENV-01', text: 'Question 1', helpText: null, domain: 'Environmental', displayOrder: 1, value: null },
      ]
      httpClient.apiFetch.mockResolvedValue(new Response(JSON.stringify(questions), { status: 200 }))

      const result = await getQuestions('diag-1')

      expect(result).toEqual(questions)
      expect(httpClient.apiFetch).toHaveBeenCalledWith('/api/diagnostics/diag-1/questions')
    })

    it('lève une ApiError sur un diagnostic introuvable (404)', async () => {
      httpClient.apiFetch.mockResolvedValue(new Response('{}', { status: 404 }))

      await expect(getQuestions('diag-inconnu')).rejects.toBeInstanceOf(ApiError)
    })
  })

  describe('upsertResponse', () => {
    it('envoie la valeur en PUT et retourne la réponse enregistrée', async () => {
      httpClient.apiFetch.mockResolvedValue(
        new Response(
          JSON.stringify({
            id: 'resp-1',
            diagnosticId: 'diag-1',
            questionId: 'q-1',
            value: 3,
            answeredAt: '2026-01-01T00:00:00Z',
          }),
          { status: 200 },
        ),
      )

      const result = await upsertResponse('diag-1', 'ENV-01', 3)

      expect(result.value).toBe(3)
      const [path, init] = httpClient.apiFetch.mock.calls[0]
      expect(path).toBe('/api/diagnostics/diag-1/responses/ENV-01')
      expect(init?.method).toBe('PUT')
      expect(JSON.parse(init?.body as string)).toEqual({ value: 3 })
    })

    it('lève une ApiError si le diagnostic n’est plus InProgress (409)', async () => {
      httpClient.apiFetch.mockResolvedValue(
        new Response(JSON.stringify({ message: 'Diagnostic non modifiable.' }), { status: 409 }),
      )

      await expect(upsertResponse('diag-1', 'ENV-01', 3)).rejects.toThrow('Diagnostic non modifiable.')
    })
  })

  describe('complete', () => {
    it('finalise le diagnostic', async () => {
      httpClient.apiFetch.mockResolvedValue(
        new Response(
          JSON.stringify({
            id: 'diag-1',
            companyId: 'company-1',
            status: 'Completed',
            globalScore: 3.5,
            createdAt: '2026-01-01T00:00:00Z',
            completedAt: '2026-01-01T00:20:00Z',
          }),
          { status: 200 },
        ),
      )

      const result = await complete('diag-1')

      expect(result.status).toBe('Completed')
      const [path, init] = httpClient.apiFetch.mock.calls[0]
      expect(path).toBe('/api/diagnostics/diag-1/complete')
      expect(init?.method).toBe('POST')
    })

    it('lève une IncompleteQuestionnaireError portant les codes manquants sur 400', async () => {
      httpClient.apiFetch.mockResolvedValue(
        new Response(
          JSON.stringify({ message: 'Questions manquantes.', missingQuestionCodes: ['ENV-02', 'ENV-03'] }),
          { status: 400 },
        ),
      )

      const error = await complete('diag-1').catch((e) => e)

      expect(error).toBeInstanceOf(IncompleteQuestionnaireError)
      expect((error as IncompleteQuestionnaireError).missingQuestionCodes).toEqual(['ENV-02', 'ENV-03'])
    })
  })
})
