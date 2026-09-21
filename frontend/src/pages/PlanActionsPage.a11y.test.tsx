import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { axe } from 'vitest-axe'

const dashboardApi = vi.hoisted(() => ({ getDashboard: vi.fn() }))
vi.mock('../api/dashboardApi', () => dashboardApi)

const actionPlanApiMock = vi.hoisted(() => ({
  getActionPlan: vi.fn(),
  upsertActionItemProgress: vi.fn(),
}))
vi.mock('../api/actionPlanApi', () => actionPlanApiMock)

import { PlanActionsPage } from './PlanActionsPage'
import { useAuthStore } from '../store/authStore'
import { makeDashboardView, makeLatestDiagnostic, resetDashboardStore } from '../test/dashboardFixtures'
import { makeActionItemWithProgress, resetPlanActionsStore } from '../test/planActionsFixtures'

function renderPage() {
  return render(
    <MemoryRouter>
      <PlanActionsPage />
    </MemoryRouter>,
  )
}

describe('PlanActionsPage (axe)', () => {
  beforeEach(() => {
    resetDashboardStore()
    resetPlanActionsStore()
    dashboardApi.getDashboard.mockReset()
    actionPlanApiMock.getActionPlan.mockReset()
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Admin' }, error: null })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('aucun diagnostic complété : aucune violation détectable', async () => {
    dashboardApi.getDashboard.mockResolvedValue(makeDashboardView({ hasCompletedDiagnostic: false }))

    const { container } = renderPage()
    await screen.findByRole('link', { name: 'Commencer le questionnaire' })

    expect(await axe(container)).toHaveNoViolations()
  })

  it('aucune recommandation déclenchée : aucune violation détectable', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    actionPlanApiMock.getActionPlan.mockResolvedValue([])

    const { container } = renderPage()
    await screen.findByText(/bravo/i)

    expect(await axe(container)).toHaveNoViolations()
  })

  it('liste de recommandations : aucune violation détectable', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    actionPlanApiMock.getActionPlan.mockResolvedValue([
      makeActionItemWithProgress({ code: 'REC-ENV-01' }),
      makeActionItemWithProgress({ code: 'REC-SOC-01', domain: 'Social', priorityRank: 2 }),
    ])

    const { container } = renderPage()
    await screen.findAllByRole('button', { name: /Statut/i })

    expect(await axe(container)).toHaveNoViolations()
  })
})
