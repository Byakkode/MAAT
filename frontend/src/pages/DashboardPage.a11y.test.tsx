import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { axe } from 'vitest-axe'

const dashboardApi = vi.hoisted(() => ({ getDashboard: vi.fn() }))
vi.mock('../api/dashboardApi', () => dashboardApi)

const recommendationsApi = vi.hoisted(() => ({ updateRecommendationProgress: vi.fn() }))
vi.mock('../api/recommendationsApi', () => recommendationsApi)

import { DashboardPage } from './DashboardPage'
import { useAuthStore } from '../store/authStore'
import {
  makeAllDomainScores,
  makeActionPlan,
  makeDashboardView,
  makeHistoryPoint,
  makeInProgressDiagnostic,
  makeLatestDiagnostic,
  makeRecommendation,
  resetDashboardStore,
} from '../test/dashboardFixtures'

// docs/specs/dashboard.md, cas 20 : test axe sur l'écran complet, dans les trois états de la
// section 1 — chacun est un écran à part entière, pas un cas d'erreur, et doit donc être audité
// individuellement plutôt que de supposer qu'un seul passage suffit pour les trois.
describe('DashboardPage (axe, trois états)', () => {
  beforeEach(() => {
    resetDashboardStore()
    dashboardApi.getDashboard.mockReset()
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Admin' }, error: null })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('état 1 (aucun diagnostic) : aucune violation détectable', async () => {
    dashboardApi.getDashboard.mockResolvedValue(makeDashboardView())

    const { container } = render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>,
    )
    await waitFor(() => expect(dashboardApi.getDashboard).toHaveBeenCalled())
    await screen.findByRole('link', { name: /questionnaire/i })

    expect(await axe(container)).toHaveNoViolations()
  })

  it('état 2 (diagnostic en cours uniquement) : aucune violation détectable', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ inProgressDiagnostic: makeInProgressDiagnostic({ answeredCount: 10, totalActiveQuestions: 45 }) }),
    )

    const { container } = render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>,
    )
    await screen.findByText(/10/)

    expect(await axe(container)).toHaveNoViolations()
  })

  it('état 3 (diagnostic complété, avec historique, radar et plan d’actions) : aucune violation détectable', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({
        hasCompletedDiagnostic: true,
        latestDiagnostic: makeLatestDiagnostic({ globalScore: 62.3, sectorCode: '4941A' }),
        domainScores: makeAllDomainScores(62),
        history: [
          makeHistoryPoint({ completedAt: '2026-02-10T09:24:00Z', globalScore: 20, deltaFromPrevious: null }),
          makeHistoryPoint({ completedAt: '2026-05-18T14:22:00Z', globalScore: 62.3, deltaFromPrevious: 42.3 }),
        ],
        actionPlan: makeActionPlan({
          items: [
            makeRecommendation({ code: 'REC-ENV-01', priorityRank: 1 }),
            makeRecommendation({ code: 'REC-SOC-01', priorityRank: 2, isCompleted: true }),
          ],
          totalCount: 7,
          completedCount: 1,
        }),
        inProgressDiagnostic: makeInProgressDiagnostic(),
      }),
    )

    const { container } = render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>,
    )
    await screen.findByText('Démarche structurée')

    expect(await axe(container)).toHaveNoViolations()
  })
})
