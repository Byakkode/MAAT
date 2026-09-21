import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'

const dashboardApi = vi.hoisted(() => ({ getDashboard: vi.fn() }))
vi.mock('../api/dashboardApi', () => dashboardApi)

const recommendationsApi = vi.hoisted(() => ({
  getRecommendations: vi.fn(),
  updateRecommendationProgress: vi.fn(),
}))
vi.mock('../api/recommendationsApi', () => recommendationsApi)

import { PlanActionsPage } from './PlanActionsPage'
import { useAuthStore } from '../store/authStore'
import { makeDashboardView, makeLatestDiagnostic, resetDashboardStore } from '../test/dashboardFixtures'
import { makeRecommendationDetail, resetPlanActionsStore } from '../test/planActionsFixtures'

function renderPage() {
  return render(
    <MemoryRouter>
      <PlanActionsPage />
    </MemoryRouter>,
  )
}

// docs/specs/recommandations.md, section 4.
describe('PlanActionsPage', () => {
  beforeEach(() => {
    resetDashboardStore()
    resetPlanActionsStore()
    dashboardApi.getDashboard.mockReset()
    recommendationsApi.getRecommendations.mockReset()
    recommendationsApi.updateRecommendationProgress.mockReset()
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Admin' }, error: null })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('affiche un état de chargement tant que le tableau de bord n’est pas résolu', () => {
    dashboardApi.getDashboard.mockReturnValue(new Promise(() => {}))

    renderPage()

    expect(screen.getByRole('status').textContent).toBe("Chargement du plan d'actions…")
  })

  it('compte sans diagnostic complété → invitation à en démarrer un, même après un diagnostic terminé ailleurs et jamais rechargé', async () => {
    // Reproduit le bug diagnostiqué en E2E (e2e/plan-actions.spec.ts) : useDashboardStore n'est
    // rechargé qu'une fois par session par AppShell (idle-guardé, pour le badge de la barre
    // latérale) — un diagnostic complété entre-temps sur un autre écran laisserait
    // hasCompletedDiagnostic périmé sans le rechargement inconditionnel propre à cet écran.
    dashboardApi.getDashboard.mockResolvedValue(makeDashboardView({ hasCompletedDiagnostic: false }))

    renderPage()

    await screen.findByText(/vous n'avez pas encore de diagnostic complété/i)
    expect(screen.getByRole('link', { name: 'Commencer le questionnaire' })).toBeDefined()
    expect(recommendationsApi.getRecommendations).not.toHaveBeenCalled()
  })

  it('aucune recommandation déclenchée → message de félicitation, pas une erreur', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    recommendationsApi.getRecommendations.mockResolvedValue([])

    renderPage()

    await screen.findByText(/bravo/i)
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('affiche la liste des recommandations triée par le serveur, avec domaine, effort et impact', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    recommendationsApi.getRecommendations.mockResolvedValue([
      makeRecommendationDetail({ code: 'REC-ENV-01', priorityRank: 1, actionText: 'Action prioritaire' }),
      makeRecommendationDetail({
        code: 'REC-SOC-01',
        priorityRank: 2,
        actionText: 'Action secondaire',
        domain: 'Social',
        effortLevel: 'Low',
        impactPoints: 3,
        isCompleted: true,
        completedAt: '2026-03-01T10:00:00Z',
      }),
    ])

    renderPage()

    const items = await screen.findAllByRole('checkbox')
    expect(items).toHaveLength(2)
    expect(screen.getByText('Action prioritaire')).toBeDefined()
    expect(screen.getByText('Action secondaire')).toBeDefined()
    expect(screen.getByText(/Environnement · Effort modéré · 5 points/)).toBeDefined()
    expect(screen.getByText(/Social & droits humains · Effort faible · 3 points/)).toBeDefined()
    expect(screen.getByText(/Terminée le 1 mars 2026/)).toBeDefined()
    expect(screen.getByText((_, el) => el?.tagName === 'P' && /1\s*\/\s*2 actions/.test(el.textContent ?? ''))).toBeDefined()
  })

  it('Admin/User peuvent cocher une action, qui recharge la liste', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    recommendationsApi.getRecommendations
      .mockResolvedValueOnce([makeRecommendationDetail({ code: 'REC-ENV-01', isCompleted: false })])
      .mockResolvedValueOnce([makeRecommendationDetail({ code: 'REC-ENV-01', isCompleted: true, completedAt: '2026-03-01T10:00:00Z' })])
    recommendationsApi.updateRecommendationProgress.mockResolvedValue({})

    renderPage()

    const checkbox = await screen.findByRole('checkbox')
    await userEvent.click(checkbox)

    await waitFor(() => expect(recommendationsApi.updateRecommendationProgress).toHaveBeenCalledWith('diag-1', 'REC-ENV-01', true))
    await screen.findByText(/terminée le 1 mars 2026/i)
  })

  it('Viewer voit des cases désactivées, jamais actionnables', async () => {
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Viewer' }, error: null })
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    recommendationsApi.getRecommendations.mockResolvedValue([makeRecommendationDetail()])

    renderPage()

    const checkbox = await screen.findByRole('checkbox')
    expect(checkbox.hasAttribute('disabled')).toBe(true)
  })
})
