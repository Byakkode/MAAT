import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

const dashboardApi = vi.hoisted(() => ({ getDashboard: vi.fn() }))
vi.mock('../api/dashboardApi', () => dashboardApi)

const recommendationsApi = vi.hoisted(() => ({ updateRecommendationProgress: vi.fn() }))
vi.mock('../api/recommendationsApi', () => recommendationsApi)

import { DashboardPage } from './DashboardPage'
import { useAuthStore } from '../store/authStore'
import {
  makeAllDomainScores,
  makeDashboardView,
  makeHistoryPoint,
  makeInProgressDiagnostic,
  makeLatestDiagnostic,
  resetDashboardStore,
} from '../test/dashboardFixtures'

function renderPage() {
  return render(
    <MemoryRouter>
      <DashboardPage />
    </MemoryRouter>,
  )
}

// docs/specs/dashboard.md, section 1 : les trois états sont des écrans à part entière, jamais
// des cas d'erreur — chacun doit produire un rendu 200 explicite, pas un fallback générique.
describe('DashboardPage', () => {
  beforeEach(() => {
    resetDashboardStore()
    dashboardApi.getDashboard.mockReset()
    recommendationsApi.updateRecommendationProgress.mockReset()
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Admin' }, error: null })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it("état 1 — aucun diagnostic : aucune erreur, invite à démarrer", async () => {
    dashboardApi.getDashboard.mockResolvedValue(makeDashboardView())

    renderPage()

    await waitFor(() => expect(screen.queryByRole('status')).toBeNull())

    expect(screen.queryByText(/erreur/i)).toBeNull()
    expect(screen.queryByRole('img')).toBeNull()
    expect(screen.getByRole('link', { name: /questionnaire/i })).toBeDefined()
  })

  it('état 2 — diagnostic en cours uniquement : avancement affiché, aucun score', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ inProgressDiagnostic: makeInProgressDiagnostic({ answeredCount: 10, totalActiveQuestions: 45 }) }),
    )

    renderPage()

    await waitFor(() => expect(screen.getByText(/10/)).toBeDefined())
    expect(screen.getByText(/45/)).toBeDefined()
    expect(screen.queryByRole('img')).toBeNull()
    expect(screen.queryByText(/Démarche/)).toBeNull()
  })

  it('état 3 — au moins un diagnostic complété : score, radar et plan d’actions affichés', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({
        hasCompletedDiagnostic: true,
        latestDiagnostic: makeLatestDiagnostic({ globalScore: 62.3, sectorCode: '4941A' }),
        // Volontairement différent du score global arrondi (62) : évite une collision de
        // texte avec ScoreSummary dans les assertions ci-dessous, et reste réaliste (les
        // scores de domaine ne sont pas censés être égaux au score global pondéré).
        domainScores: makeAllDomainScores(55),
        history: [makeHistoryPoint({ globalScore: 62.3 })],
      }),
    )

    renderPage()

    await waitFor(() => expect(screen.getByText('62')).toBeDefined())
    expect(screen.getByText('Démarche structurée')).toBeDefined()
    expect(screen.getByRole('img')).toBeDefined()
    expect(screen.getByRole('table')).toBeDefined()
  })

  it("les trois sections de l'écran ne cohabitent qu'avec les données pertinentes à l'état 3 (diagnostic en cours simultané)", async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({
        hasCompletedDiagnostic: true,
        latestDiagnostic: makeLatestDiagnostic({ globalScore: 62.3 }),
        domainScores: makeAllDomainScores(62),
        history: [makeHistoryPoint({ globalScore: 62.3 })],
        inProgressDiagnostic: makeInProgressDiagnostic(),
      }),
    )

    renderPage()

    // docs/specs/dashboard.md, section 7 : le diagnostic en cours n'écrase pas le tableau de
    // bord du dernier complété — les deux coexistent.
    await waitFor(() => expect(screen.getByText('Démarche structurée')).toBeDefined())
    expect(screen.getByRole('link', { name: /reprendre/i })).toBeDefined()
  })

  it("ne fait qu'un seul appel réseau au montage", async () => {
    dashboardApi.getDashboard.mockResolvedValue(makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic() }))

    renderPage()

    await waitFor(() => expect(dashboardApi.getDashboard).toHaveBeenCalled())
    expect(dashboardApi.getDashboard).toHaveBeenCalledTimes(1)
  })
})
