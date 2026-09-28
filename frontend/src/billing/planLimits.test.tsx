import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import type { ReactNode } from 'react'

const dashboardApi = vi.hoisted(() => ({ getDashboard: vi.fn() }))
vi.mock('../api/dashboardApi', () => dashboardApi)

const billingApi = vi.hoisted(() => ({ getSubscription: vi.fn() }))
vi.mock('../api/billingApi', () => billingApi)

const actionPlanApiMock = vi.hoisted(() => ({ getActionPlan: vi.fn(), upsertActionItemProgress: vi.fn() }))
vi.mock('../api/actionPlanApi', () => actionPlanApiMock)

const recommendationsApi = vi.hoisted(() => ({ updateRecommendationProgress: vi.fn() }))
vi.mock('../api/recommendationsApi', () => recommendationsApi)

import { DashboardPage } from '../pages/DashboardPage'
import { PlanActionsPage } from '../pages/PlanActionsPage'
import { PastDueBanner } from '../components/shell/PastDueBanner'
import { PlanComparisonTable } from './PlanComparisonTable'
import { useAuthStore } from '../store/authStore'
import { useSubscriptionStore } from '../store/subscriptionStore'
import type { Entitlements } from '../api/billingApi'
import {
  makeActionPlan,
  makeAllDomainScores,
  makeRecommendation,
  makeDashboardView,
  makeHistoryPoint,
  makeLatestDiagnostic,
  resetDashboardStore,
} from '../test/dashboardFixtures'
import { makeActionItemWithProgress } from '../test/planActionsFixtures'
import {
  ESSENTIAL_ENTITLEMENTS,
  makeSubscription,
  PROFESSIONAL_ENTITLEMENTS,
  STARTER_ENTITLEMENTS,
} from '../test/subscriptionFixtures'

function withPlan(plan: 'Starter' | 'Essential' | 'Professional', entitlements: Entitlements) {
  const subscription = makeSubscription({ plan, effectivePlan: plan, entitlements })
  useSubscriptionStore.setState({ status: 'loaded', subscription })
  billingApi.getSubscription.mockResolvedValue(subscription)
}

function completedDashboard() {
  return makeDashboardView({
    hasCompletedDiagnostic: true,
    latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }),
    domainScores: makeAllDomainScores(),
    history: [makeHistoryPoint()],
    actionPlan: makeActionPlan({
      items: [makeRecommendation({ code: 'REC-1' })],
      totalCount: 3,
      triggeredCount: 20,
    }),
  })
}

function renderAt(element: ReactNode) {
  return render(<MemoryRouter>{element}</MemoryRouter>)
}

// docs/specs/abonnement.md, section 8 : l'écran reflète les droits calculés par l'API.
describe('Limites par offre', () => {
  beforeEach(() => {
    resetDashboardStore()
    dashboardApi.getDashboard.mockReset()
    billingApi.getSubscription.mockReset()
    actionPlanApiMock.getActionPlan.mockReset()
    recommendationsApi.updateRecommendationProgress.mockReset()
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Admin' }, error: null })
  })

  afterEach(() => {
    useSubscriptionStore.setState({ status: 'idle', subscription: null })
    vi.restoreAllMocks()
  })

  it('Starter : tableau de bord sans scores par domaine ni benchmark, cases en lecture seule', async () => {
    withPlan('Starter', STARTER_ENTITLEMENTS)
    dashboardApi.getDashboard.mockResolvedValue(completedDashboard())

    renderAt(<DashboardPage />)

    expect(await screen.findByText('Votre score par domaine')).toBeDefined()
    expect(screen.queryByRole('list', { name: 'Légende des domaines' })).toBeNull()
    expect(screen.getByText("Inclus dans l'offre Professional")).toBeDefined()
    expect((screen.getByRole('checkbox') as HTMLInputElement).disabled).toBe(true)
    expect(screen.getByText(/17 autres actions recommandées/)).toBeDefined()
  })

  it('Essential : scores par domaine visibles et cases actionnables', async () => {
    withPlan('Essential', ESSENTIAL_ENTITLEMENTS)
    dashboardApi.getDashboard.mockResolvedValue(completedDashboard())

    renderAt(<DashboardPage />)

    await waitFor(() => expect(screen.queryByText('Votre score par domaine')).toBeNull())
    await screen.findByRole('checkbox')
    expect((screen.getByRole('checkbox') as HTMLInputElement).disabled).toBe(false)
    // Deuxième palier : les actions au-delà des douze premières relèvent de Professional.
    expect(screen.getByText(/17 autres actions recommandées/)).toBeDefined()
    expect(screen.getByText(/Inclus à partir de l'offre Professional/)).toBeDefined()
  })

  it('Professional : toutes les actions visibles, aucune invitation', async () => {
    withPlan('Professional', PROFESSIONAL_ENTITLEMENTS)
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({
        ...completedDashboard(),
        actionPlan: makeActionPlan({ items: [makeRecommendation({ code: 'REC-1' })], totalCount: 20, triggeredCount: 20 }),
      }),
    )

    renderAt(<DashboardPage />)

    await screen.findByRole('checkbox')
    expect(screen.queryByText(/autres actions recommandées/)).toBeNull()
    expect(screen.queryByText(/Inclus à partir de l'offre/)).toBeNull()
  })

  it.each([
    ['Starter', STARTER_ENTITLEMENTS, 'readonly'],
    ['Essential', ESSENTIAL_ENTITLEMENTS, 'check'],
    ['Professional', PROFESSIONAL_ENTITLEMENTS, 'full'],
  ] as const)('Plan d’actions en %s : mode %s', async (plan, entitlements, mode) => {
    withPlan(plan, entitlements)
    dashboardApi.getDashboard.mockResolvedValue(completedDashboard())
    actionPlanApiMock.getActionPlan.mockResolvedValue([makeActionItemWithProgress()])

    renderAt(<PlanActionsPage />)

    const list = await screen.findByRole('list')
    const checkbox = within(list).queryByRole('checkbox')
    // Menu de statut (recommandations.md, section 4 bis) : actionnable en Professional
    // seulement ; ailleurs, l'étiquette seule (readonly) ou la case à cocher (check).
    const statusMenu = within(list).queryByRole('button', { name: /^Statut/ })

    expect(checkbox !== null).toBe(mode === 'check')
    expect(statusMenu !== null).toBe(mode === 'full')
    expect(within(list).queryByText('Planifié') !== null).toBe(mode !== 'check')
    expect(screen.queryByText(/offre Starter présente/) !== null).toBe(mode === 'readonly')
  })

  it('bandeau d’impayé sur PastDue, absent sinon', () => {
    useSubscriptionStore.setState({ status: 'loaded', subscription: makeSubscription({ status: 'PastDue' }) })
    const { unmount } = renderAt(<PastDueBanner />)
    expect(screen.getByRole('status').textContent).toMatch(/dernier paiement/)
    unmount()

    useSubscriptionStore.setState({ status: 'loaded', subscription: makeSubscription({ status: 'Active' }) })
    renderAt(<PastDueBanner />)
    expect(screen.queryByRole('status')).toBeNull()
  })

  it('comparatif : les fonctionnalités pas encore construites sont marquées « Bientôt »', () => {
    renderAt(<PlanComparisonTable period="monthly" renderAction={() => null} />)

    const vsmeRow = screen.getByRole('rowheader', { name: /Générateur de rapport VSME/ })
    expect(within(vsmeRow).getByText('Bientôt')).toBeDefined()
    const scoreRow = screen.getByRole('rowheader', { name: /Score RSE global/ })
    expect(within(scoreRow).queryByText('Bientôt')).toBeNull()
  })
})
