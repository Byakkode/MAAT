import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { axe } from 'vitest-axe'

// docs/specs/questionnaire.md, section 7 : exigence WCAG AA — un passage axe-core par étape,
// à la fois pour un diagnostic en cours (contrôles actifs) et pour un diagnostic Completed
// (lecture seule, contrôles désactivés).
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

const DOMAINS = ['Environmental', 'Social', 'Ethics', 'Procurement', 'Governance'] as const
const DOMAIN_LABELS: Record<(typeof DOMAINS)[number], string> = {
  Environmental: 'Environnement',
  Social: 'Social & droits humains',
  Ethics: 'Éthique des affaires',
  Procurement: 'Achats responsables',
  Governance: 'Gouvernance & pilotage',
}

function questionsForAllDomains(answered: boolean) {
  return DOMAINS.flatMap((domain, domainIndex) => [
    {
      code: `${domain.slice(0, 3).toUpperCase()}-${domainIndex}-01`,
      text: `Question 1 du domaine ${domain}`,
      helpText: "Texte d'aide pour cette question.",
      domain,
      displayOrder: 1,
      value: answered ? 3 : null,
    },
  ])
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/questionnaire/:diagnosticId?" element={<QuestionnairePage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('QuestionnairePage (axe par étape)', () => {
  beforeEach(() => {
    resetQuestionnaireStore()
    diagnosticsApi.getCurrent.mockReset()
    diagnosticsApi.getById.mockReset()
    diagnosticsApi.getQuestions.mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it.each(DOMAINS.map((domain, index) => [index, domain] as const))(
    'étape %i (%s, diagnostic en cours) : aucune violation détectable',
    async (index, domain) => {
      diagnosticsApi.getCurrent.mockResolvedValue({
        id: 'diag-1',
        companyId: 'c-1',
        status: 'InProgress',
        createdAt: '2026-01-01T00:00:00Z',
        answeredCount: 5,
        totalActiveQuestions: 5,
      })
      diagnosticsApi.getById.mockResolvedValue(makeDiagnosticDetail('InProgress'))
      diagnosticsApi.getQuestions.mockResolvedValue(questionsForAllDomains(true))

      const { container } = renderAt('/questionnaire')

      await waitFor(() => expect(useQuestionnaireStore.getState().loadStatus).toBe('loaded'))

      act(() => {
        useQuestionnaireStore.getState().goToStep(index)
      })
      await waitFor(() => expect(screen.getByRole('heading', { name: DOMAIN_LABELS[domain] })).toBeDefined())

      const results = await axe(container)

      expect(results).toHaveNoViolations()
    },
  )

  it('diagnostic Completed en lecture seule : aucune violation détectable', async () => {
    diagnosticsApi.getCurrent.mockResolvedValue(null)
    diagnosticsApi.getById.mockResolvedValue(makeDiagnosticDetail('Completed', { id: 'diag-completed' }))
    diagnosticsApi.getQuestions.mockResolvedValue(questionsForAllDomains(true))

    const { container } = renderAt('/questionnaire/diag-completed')

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Environnement' })).toBeDefined())

    const results = await axe(container)

    expect(results).toHaveNoViolations()
  })

  it('diagnostic Archived en lecture seule : aucune violation détectable', async () => {
    diagnosticsApi.getCurrent.mockResolvedValue(null)
    diagnosticsApi.getById.mockResolvedValue(makeDiagnosticDetail('Archived', { id: 'diag-archived' }))
    diagnosticsApi.getQuestions.mockResolvedValue(questionsForAllDomains(true))

    const { container } = renderAt('/questionnaire/diag-archived')

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Environnement' })).toBeDefined())

    const results = await axe(container)

    expect(results).toHaveNoViolations()
  })
})
