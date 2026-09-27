import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'

const dashboardApi = vi.hoisted(() => ({ getDashboard: vi.fn() }))
vi.mock('../api/dashboardApi', () => dashboardApi)

const actionPlanApiMock = vi.hoisted(() => ({
  getActionPlan: vi.fn(),
  upsertActionItemProgress: vi.fn(),
  getActionItemHistory: vi.fn(),
}))
vi.mock('../api/actionPlanApi', () => actionPlanApiMock)

import { PlanActionsPage } from './PlanActionsPage'
import { useAuthStore } from '../store/authStore'
import { useSubscriptionStore } from '../store/subscriptionStore'
import { ESSENTIAL_ENTITLEMENTS, PROFESSIONAL_ENTITLEMENTS, makeSubscription } from '../test/subscriptionFixtures'
import { makeDashboardView, makeLatestDiagnostic, resetDashboardStore } from '../test/dashboardFixtures'
import { makeActionItemWithProgress, resetPlanActionsStore } from '../test/planActionsFixtures'

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
    actionPlanApiMock.getActionPlan.mockReset()
    actionPlanApiMock.upsertActionItemProgress.mockReset()
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Admin' }, error: null })
  })

  afterEach(() => {
    vi.restoreAllMocks()
    useSubscriptionStore.setState({ status: 'idle', subscription: null })
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

    await screen.findByText(/vous n'avez pas encore de diagnostic compl/i)
    expect(screen.getByRole('link', { name: 'Commencer le questionnaire' })).toBeDefined()
    expect(actionPlanApiMock.getActionPlan).not.toHaveBeenCalled()
  })

  it('aucune recommandation déclenchée → message de félicitation, pas une erreur', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    actionPlanApiMock.getActionPlan.mockResolvedValue([])

    renderPage()

    await screen.findByText(/bravo/i)
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('affiche la liste des actions triée par le serveur, avec domaine, effort et impact', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    actionPlanApiMock.getActionPlan.mockResolvedValue([
      makeActionItemWithProgress({ code: 'REC-ENV-01', priorityRank: 1, actionText: 'Action prioritaire' }),
      makeActionItemWithProgress({
        code: 'REC-SOC-01',
        priorityRank: 2,
        actionText: 'Action secondaire',
        domain: 'Social',
        effortLevel: 'Low',
        impactPoints: 3,
        isCompleted: true,
        completedAt: '2026-03-01T10:00:00Z',
        status: 'Done',
      }),
    ])

    renderPage()

    const statusBtns = await screen.findAllByRole('button', { name: /Statut/i })
    expect(statusBtns).toHaveLength(2)
    expect(screen.getByText('Action prioritaire')).toBeDefined()
    expect(screen.getByText('Action secondaire')).toBeDefined()
    expect(screen.getByText(/Environnement · Effort modéré · 5 points/)).toBeDefined()
    expect(screen.getByText(/Social & droits humains · Effort faible · 3 points/)).toBeDefined()
    expect(screen.getByText(/Terminée le 1 mars 2026/)).toBeDefined()
    expect(screen.getByText((_, el) => el?.tagName === 'P' && /1\s*\/\s*2 actions/.test(el.textContent ?? ''))).toBeDefined()
  })

  it('Admin/User peuvent changer le statut d’une action', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    actionPlanApiMock.getActionPlan.mockResolvedValue([
      makeActionItemWithProgress({ code: 'REC-ENV-01', status: 'Planned', assignedTo: null, dueDate: null, notes: null }),
    ])
    actionPlanApiMock.upsertActionItemProgress.mockResolvedValue({
      diagnosticId: 'diag-1',
      code: 'REC-ENV-01',
      status: 'InProgress',
      assignedTo: null,
      dueDate: null,
      notes: null,
      progressUpdatedAt: '2026-09-21T10:00:00Z',
    })

    renderPage()

    const statusBtn = await screen.findByRole('button', { name: /Statut : Planifié/i })
    await userEvent.click(statusBtn)

    await waitFor(() =>
      expect(actionPlanApiMock.upsertActionItemProgress).toHaveBeenCalledWith(
        'diag-1',
        'REC-ENV-01',
        { status: 'InProgress', assignedTo: null, dueDate: null, notes: null },
      )
    )
    await screen.findByRole('button', { name: /Statut : En cours/i })
  })

  it('Viewer voit des boutons de statut désactivés, jamais actionnables', async () => {
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Viewer' }, error: null })
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    actionPlanApiMock.getActionPlan.mockResolvedValue([makeActionItemWithProgress()])

    renderPage()

    const statusBtn = await screen.findByRole('button', { name: /Statut/i })
    expect(statusBtn.hasAttribute('disabled')).toBe(true)
  })

  // docs/specs/recommandations.md, section 4 bis : historique dans le détail de chaque action,
  // en Professional seulement.
  it('Professional : l’historique s’ouvre depuis le détail d’une action', async () => {
    useSubscriptionStore.setState({ status: 'loaded', subscription: makeSubscription({ entitlements: PROFESSIONAL_ENTITLEMENTS }) })
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    actionPlanApiMock.getActionPlan.mockResolvedValue([makeActionItemWithProgress({ code: 'REC-ENV-01' })])
    actionPlanApiMock.getActionItemHistory.mockResolvedValue([
      { field: 'Status', oldValue: 'Planned', newValue: 'InProgress', changedAt: '2026-10-12T12:05:00Z', changedBy: 'claire@entreprise.test' },
    ])

    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Modifier les détails' }))
    await userEvent.click(screen.getByRole('button', { name: "Voir l'historique" }))

    expect(await screen.findByText('Statut : Planifié → En cours')).toBeDefined()
    expect(actionPlanApiMock.getActionItemHistory).toHaveBeenCalledWith('diag-1', 'REC-ENV-01')
  })

  it('Essential : pas d’historique', async () => {
    useSubscriptionStore.setState({ status: 'loaded', subscription: makeSubscription({ effectivePlan: 'Essential', entitlements: ESSENTIAL_ENTITLEMENTS }) })
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    actionPlanApiMock.getActionPlan.mockResolvedValue([makeActionItemWithProgress({ detailText: 'Détail de l’action.' })])

    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Voir les détails' }))

    expect(screen.getByText('Détail de l’action.')).toBeDefined()
    expect(screen.queryByRole('button', { name: "Voir l'historique" })).toBeNull()
  })
})
