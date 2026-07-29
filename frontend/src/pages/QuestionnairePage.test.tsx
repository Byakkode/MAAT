import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

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

import { QuestionnairePage } from './QuestionnairePage'
import { useQuestionnaireStore } from '../store/questionnaireStore'
import { makeDiagnosticDetail, resetQuestionnaireStore } from '../test/questionnaireFixtures'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/questionnaire/:diagnosticId?" element={<QuestionnairePage />} />
      </Routes>
    </MemoryRouter>,
  )
}

const IN_PROGRESS_SUMMARY = {
  id: 'diag-1',
  companyId: 'c-1',
  status: 'InProgress' as const,
  createdAt: '2026-01-01T00:00:00Z',
  answeredCount: 2,
  totalActiveQuestions: 3,
}

const ENV_QUESTIONS_PARTIAL = [
  { code: 'ENV-01', text: 'Question environnement 1', helpText: null, domain: 'Environmental', displayOrder: 1, value: 4 },
  { code: 'ENV-02', text: 'Question environnement 2', helpText: null, domain: 'Environmental', displayOrder: 2, value: null },
]

const SOCIAL_QUESTION = {
  code: 'SOC-01',
  text: 'Question sociale 1',
  helpText: null,
  domain: 'Social',
  displayOrder: 1,
  value: 3,
}

describe('QuestionnairePage', () => {
  beforeEach(() => {
    resetQuestionnaireStore()
    diagnosticsApi.getCurrent.mockReset()
    diagnosticsApi.getById.mockReset()
    diagnosticsApi.getQuestions.mockReset()
    diagnosticsApi.upsertResponse.mockReset()
    diagnosticsApi.complete.mockReset()
    // Statut par défaut pour les tests qui ne portent pas sur le mode d'affichage lui-même.
    diagnosticsApi.getById.mockResolvedValue(makeDiagnosticDetail('InProgress'))
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  it('affiche un état de chargement pendant la récupération des questions', async () => {
    let resolveGetCurrent!: (value: typeof IN_PROGRESS_SUMMARY) => void
    diagnosticsApi.getCurrent.mockReturnValue(new Promise((resolve) => (resolveGetCurrent = resolve)))
    diagnosticsApi.getQuestions.mockResolvedValue(ENV_QUESTIONS_PARTIAL)

    renderAt('/questionnaire')

    expect(screen.getByRole('status').textContent).toMatch(/chargement/i)

    await act(async () => {
      resolveGetCurrent(IN_PROGRESS_SUMMARY)
    })
  })

  it('reprise : arrive à la première étape comportant une question sans réponse', async () => {
    diagnosticsApi.getCurrent.mockResolvedValue(IN_PROGRESS_SUMMARY)
    diagnosticsApi.getQuestions.mockResolvedValue([...ENV_QUESTIONS_PARTIAL, SOCIAL_QUESTION])

    renderAt('/questionnaire')

    // ENV-02 (2e question du domaine Environnement, seule étape existante ici) n'a pas de
    // réponse : l'utilisateur doit rester sur l'étape Environnement, pas être renvoyé à zéro.
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Environnement' })).toBeDefined())
    expect(screen.queryByRole('heading', { name: 'Social & droits humains' })).toBeNull()
  })

  it('bloque le passage à l’étape suivante tant qu’une sauvegarde est en échec', async () => {
    diagnosticsApi.getCurrent.mockResolvedValue(IN_PROGRESS_SUMMARY)
    diagnosticsApi.getQuestions.mockResolvedValue([...ENV_QUESTIONS_PARTIAL, SOCIAL_QUESTION])

    renderAt('/questionnaire')

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Environnement' })).toBeDefined())

    act(() => {
      useQuestionnaireStore.setState({ saveStatus: { 'ENV-01': 'error' } })
    })

    const nextButton = screen.getByRole('button', { name: 'Suivant' })
    expect(nextButton).toHaveProperty('disabled', true)

    fireEvent.click(nextButton)

    expect(screen.getByRole('heading', { name: 'Environnement' })).toBeDefined()
    expect(screen.queryByRole('heading', { name: 'Social & droits humains' })).toBeNull()
  })

  it('un diagnostic Completed s’affiche en lecture seule, sans contrôle actif, avec sa date de complétion', async () => {
    diagnosticsApi.getCurrent.mockResolvedValue(null)
    diagnosticsApi.getById.mockResolvedValue(
      makeDiagnosticDetail('Completed', { id: 'diag-completed', completedAt: '2026-03-15T10:00:00Z' }),
    )
    diagnosticsApi.getQuestions.mockResolvedValue([
      { ...ENV_QUESTIONS_PARTIAL[0], value: 4 },
      { ...ENV_QUESTIONS_PARTIAL[1], value: 2 },
    ])

    renderAt('/questionnaire/diag-completed')

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Environnement' })).toBeDefined())

    expect(diagnosticsApi.getById).toHaveBeenCalledWith('diag-completed')
    expect(screen.getByText(/diagnostic terminé le 15 mars 2026/i)).toBeDefined()
    for (const radio of screen.getAllByRole('radio')) {
      expect((radio as HTMLInputElement).disabled).toBe(true)
    }
    expect(screen.queryByRole('button', { name: 'Terminer' })).toBeNull()
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('un diagnostic Archived s’affiche en lecture seule, avec un message distinct', async () => {
    diagnosticsApi.getCurrent.mockResolvedValue(null)
    diagnosticsApi.getById.mockResolvedValue(makeDiagnosticDetail('Archived', { id: 'diag-archived' }))
    diagnosticsApi.getQuestions.mockResolvedValue(ENV_QUESTIONS_PARTIAL)

    renderAt('/questionnaire/diag-archived')

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Environnement' })).toBeDefined())

    expect(screen.getByText('Diagnostic abandonné.')).toBeDefined()
    expect(screen.queryByText(/diagnostic terminé le/i)).toBeNull()
    for (const radio of screen.getAllByRole('radio')) {
      expect((radio as HTMLInputElement).disabled).toBe(true)
    }
  })

  it('modifier une réponse re-rend la page (answeredCount change) sans re-rendre les questions voisines', async () => {
    // Contrairement au test équivalent dans QuestionStep.test.tsx, ce test monte le vrai
    // parent : QuestionnairePage s'abonne à selectAnsweredCount, qui change à chaque réponse
    // et déclenche donc réellement son re-rendu, reconstruisant les éléments <QuestionItem>
    // de l'étape. C'est le seul niveau où retirer React.memo fait échouer ce test — vérifié
    // par mutation : sans memo, le compteur de rendu d'ENV-02 augmente aussi.
    diagnosticsApi.getCurrent.mockResolvedValue({ ...IN_PROGRESS_SUMMARY, answeredCount: 0, totalActiveQuestions: 2 })
    diagnosticsApi.getQuestions.mockResolvedValue([
      { ...ENV_QUESTIONS_PARTIAL[0], value: null },
      { ...ENV_QUESTIONS_PARTIAL[1], value: null },
    ])

    renderAt('/questionnaire')

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Environnement' })).toBeDefined())

    function renderCountOf(questionText: string): string | null {
      const fieldset = screen.getByText(questionText).closest('fieldset')
      return fieldset?.getAttribute('data-render-count') ?? null
    }

    const initialEnv01 = renderCountOf('Question environnement 1')
    const initialEnv02 = renderCountOf('Question environnement 2')

    // Timers factices pour ne pas laisser le vrai anti-rebond (500 ms) déclencher un appel
    // réseau après la fin du test ; on n'a besoin que de la mise à jour synchrone de setAnswer.
    vi.useFakeTimers()
    const group = screen.getByRole('radiogroup', { name: 'Question environnement 1' })
    fireEvent.click(within(group).getByRole('radio', { name: 'Nous y réfléchissons' }))

    expect(renderCountOf('Question environnement 1')).not.toBe(initialEnv01)
    expect(renderCountOf('Question environnement 2')).toBe(initialEnv02)
  })

  it('parcours complet : répondre à toutes les questions permet de terminer le diagnostic', async () => {
    diagnosticsApi.getCurrent.mockResolvedValue({ ...IN_PROGRESS_SUMMARY, answeredCount: 0, totalActiveQuestions: 1 })
    diagnosticsApi.getQuestions.mockResolvedValue([{ ...ENV_QUESTIONS_PARTIAL[0], value: null }])
    diagnosticsApi.upsertResponse.mockResolvedValue({
      id: 'r-1',
      diagnosticId: 'diag-1',
      questionId: 'q-1',
      value: 5,
      answeredAt: '2026-01-01T00:00:00Z',
    })
    diagnosticsApi.complete.mockResolvedValue(makeDiagnosticDetail('Completed', { completedAt: '2026-01-01T00:20:00Z' }))

    renderAt('/questionnaire')

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Environnement' })).toBeDefined())

    const group = screen.getByRole('radiogroup', { name: 'Question environnement 1' })
    fireEvent.click(within(group).getByRole('radio', { name: 'Pleinement en place et suivi' }))

    // La complétion (section 6) exige que la réponse soit enregistrée côté serveur avant que
    // la précondition "toutes les questions ont une réponse" ne soit satisfaite ; on avance
    // l'état local directement plutôt que d'attendre l'anti-rebond de 500 ms ici, déjà couvert
    // par questionnaireStore.test.ts.
    await act(async () => {
      useQuestionnaireStore.setState({ saveStatus: { 'ENV-01': 'saved' } })
    })

    const completeButton = await screen.findByRole('button', { name: 'Terminer' })
    expect(completeButton).toHaveProperty('disabled', false)

    fireEvent.click(completeButton)

    await waitFor(() => expect(diagnosticsApi.complete).toHaveBeenCalledWith('diag-1'))
    await waitFor(() => expect(screen.getByText(/diagnostic complété/i)).toBeDefined())
  })
})
