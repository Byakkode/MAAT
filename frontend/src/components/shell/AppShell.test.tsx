import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { axe } from 'vitest-axe'

const dashboardApi = vi.hoisted(() => ({ getDashboard: vi.fn() }))
vi.mock('../../api/dashboardApi', () => dashboardApi)

const recommendationsApi = vi.hoisted(() => ({ updateRecommendationProgress: vi.fn() }))
vi.mock('../../api/recommendationsApi', () => recommendationsApi)

const accountApi = vi.hoisted(() => ({ getCurrentUser: vi.fn() }))
vi.mock('../../api/accountApi', () => accountApi)

import { AppShell } from './AppShell'
import { DashboardPage } from '../../pages/DashboardPage'
import { useAuthStore } from '../../store/authStore'
import {
  makeActionPlan,
  makeAllDomainScores,
  makeDashboardView,
  makeHistoryPoint,
  makeInProgressDiagnostic,
  makeLatestDiagnostic,
  makeRecommendation,
  resetDashboardStore,
} from '../../test/dashboardFixtures'
import { makeCurrentUser, resetCurrentUserStore } from '../../test/currentUserFixtures'

function renderShellAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<h1>Tableau de bord (écran)</h1>} />
          <Route path="/rapport" element={<h1>Rapports (écran)</h1>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

function renderShellWithDashboard() {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<DashboardPage />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

// docs/specs/coquille-et-compte.md, cas de test 1 à 8.
describe('AppShell', () => {
  beforeEach(() => {
    resetDashboardStore()
    resetCurrentUserStore()
    dashboardApi.getDashboard.mockReset().mockResolvedValue(makeDashboardView())
    accountApi.getCurrentUser.mockReset().mockResolvedValue(makeCurrentUser())
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Admin' }, error: null })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it("cas 1 : la navigation ne se remonte pas en changeant d’écran", async () => {
    renderShellAt('/')
    await screen.findByText('Tableau de bord (écran)')
    const navBefore = screen.getByRole('navigation', { name: 'Navigation principale' })

    await userEvent.click(screen.getByRole('link', { name: /rapports/i }))

    await screen.findByText('Rapports (écran)')
    const navAfter = screen.getByRole('navigation', { name: 'Navigation principale' })
    expect(navAfter).toBe(navBefore)
  })

  it("cas 2 : l’entrée active se signale par deux moyens distincts", async () => {
    renderShellAt('/')
    await screen.findByText('Tableau de bord (écran)')

    const active = screen.getByRole('link', { name: /tableau de bord/i })
    const inactive = screen.getByRole('link', { name: /rapports/i })

    // docs/specs/coquille-et-compte.md : fond bg-white/10 + graisse font-semibold (non chromatique).
    expect(active.className).toContain('bg-white/10')
    expect(active.className).toContain('font-semibold')
    expect(inactive.className).not.toContain('bg-white/10')
    expect(inactive.className).not.toContain('font-semibold')
  })

  it("cas 3 : un diagnostic InProgress affiche son avancement dans l’entrée Diagnostic", async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ inProgressDiagnostic: makeInProgressDiagnostic({ answeredCount: 23, totalActiveQuestions: 45 }) }),
    )

    renderShellAt('/')

    await screen.findByText('23 / 45')
  })

  it('cas 4 : Viewer voit les cinq entrées de navigation', async () => {
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Viewer' }, error: null })

    renderShellAt('/')
    await screen.findByText('Tableau de bord (écran)')

    // Le titre "Mon compte" apparaît aussi dans le menu de l'en-tête (Header) : la portée sur
    // la barre latérale seule évite l'ambiguïté entre les deux liens.
    const sidebar = screen.getByRole('navigation', { name: 'Navigation principale' })

    expect(within(sidebar).getByRole('link', { name: 'Tableau de bord' })).toBeDefined()
    expect(within(sidebar).getByRole('link', { name: 'Diagnostic' })).toBeDefined()
    expect(within(sidebar).getByRole('link', { name: "Plan d'actions" })).toBeDefined()
    expect(within(sidebar).getByRole('link', { name: 'Rapports' })).toBeDefined()
    expect(within(sidebar).getByRole('link', { name: 'Mon compte' })).toBeDefined()
  })

  it("cas 5 : le nom de l’application renvoie au tableau de bord", async () => {
    renderShellAt('/rapport')
    await screen.findByText('Rapports (écran)')

    const wordmark = screen.getByRole('link', { name: /MAAT/ })
    expect(wordmark.getAttribute('href')).toBe('/')
  })

  it("cas 6 : lien d’évitement présent, atteignable au premier Tab, menant au contenu principal", async () => {
    renderShellAt('/')
    await screen.findByText('Tableau de bord (écran)')

    await userEvent.tab()

    const skipLink = screen.getByRole('link', { name: /aller au contenu principal/i })
    expect(document.activeElement).toBe(skipLink)
    expect(skipLink.getAttribute('href')).toBe('#contenu-principal')
    expect(document.getElementById('contenu-principal')).not.toBeNull()
  })

  it('cas 7 : le panneau mobile piège le focus et se ferme à Échap', async () => {
    renderShellAt('/')
    await screen.findByText('Tableau de bord (écran)')

    await userEvent.click(screen.getByRole('button', { name: /ouvrir la navigation/i }))

    const dialog = await screen.findByRole('dialog', { name: 'Navigation' })
    const closeButton = screen.getByRole('button', { name: /fermer la navigation/i })
    expect(document.activeElement).toBe(closeButton)

    // Boucle Maj+Tab depuis le premier élément focalisable vers le dernier, sans sortir du panneau.
    await userEvent.tab({ shift: true })
    const linksInDialog = within(dialog).getAllByRole('link')
    expect(document.activeElement).toBe(linksInDialog[linksInDialog.length - 1])

    await userEvent.keyboard('{Escape}')

    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Navigation' })).toBeNull())
  })

  it('cas 8, état 1 (aucun diagnostic) : test axe sur la coquille et le tableau de bord', async () => {
    dashboardApi.getDashboard.mockResolvedValue(makeDashboardView())
    const { container } = renderShellWithDashboard()
    await screen.findByRole('link', { name: /questionnaire/i })

    expect(await axe(container)).toHaveNoViolations()
  })

  it('cas 8, état 2 (diagnostic en cours uniquement) : test axe sur la coquille et le tableau de bord', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ inProgressDiagnostic: makeInProgressDiagnostic({ answeredCount: 10, totalActiveQuestions: 45 }) }),
    )
    const { container } = renderShellWithDashboard()
    // "10 / 45" apparaît deux fois : le badge de la barre latérale et le bandeau du tableau de
    // bord (InProgressBanner) — les deux se chargent depuis le même useDashboardStore.
    await screen.findAllByText(/10/)

    expect(await axe(container)).toHaveNoViolations()
  })

  it('cas 8, état 3 (diagnostic complété) : test axe sur la coquille et le tableau de bord', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({
        hasCompletedDiagnostic: true,
        latestDiagnostic: makeLatestDiagnostic({ globalScore: 62.3, sectorCode: '4941A' }),
        domainScores: makeAllDomainScores(62),
        history: [makeHistoryPoint({ completedAt: '2026-05-18T14:22:00Z', globalScore: 62.3, deltaFromPrevious: 12 })],
        actionPlan: makeActionPlan({
          items: [makeRecommendation({ code: 'REC-ENV-01', priorityRank: 1 })],
          totalCount: 3,
          completedCount: 0,
        }),
      }),
    )
    const { container } = renderShellWithDashboard()
    await screen.findByText('Démarche structurée')

    expect(await axe(container)).toHaveNoViolations()
  })
})
