import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
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

    const statusBtns = await screen.findAllByRole('button', { name: /^Statut/ })
    expect(statusBtns).toHaveLength(2)
    expect(screen.getByText('Action prioritaire')).toBeDefined()
    expect(screen.getByText('Action secondaire')).toBeDefined()
    expect(screen.getByText(/Environnement · Effort modéré · 5 points/)).toBeDefined()
    expect(screen.getByText(/Social & droits humains · Effort faible · 3 points/)).toBeDefined()
    expect(screen.getByText(/Terminée le 1 mars 2026/)).toBeDefined()
    expect(screen.getByText((_, el) => el?.tagName === 'P' && /1\s*\/\s*2 actions/.test(el.textContent ?? ''))).toBeDefined()
  })

  // docs/specs/recommandations.md, section 4 bis : le statut choisi dans le menu de l'étiquette
  // part seul et aussitôt, avec les autres champs à leur valeur enregistrée.
  it('Admin/User passent une action de Planifié à Terminé en un seul changement', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    actionPlanApiMock.getActionPlan.mockResolvedValue([
      makeActionItemWithProgress({ code: 'REC-ENV-01', status: 'Planned', assignedTo: 'Paul', dueDate: null, notes: null }),
    ])
    actionPlanApiMock.upsertActionItemProgress.mockResolvedValue({
      diagnosticId: 'diag-1',
      code: 'REC-ENV-01',
      status: 'Done',
      assignedTo: 'Paul',
      dueDate: null,
      notes: null,
      progressUpdatedAt: '2026-09-21T10:00:00Z',
      completedAt: '2026-09-21T10:00:00Z',
    })

    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: /Statut : Planifié/ }))
    await userEvent.click(await screen.findByRole('menuitemradio', { name: 'Terminé' }))

    await waitFor(() => expect(actionPlanApiMock.upsertActionItemProgress).toHaveBeenCalledOnce())
    expect(actionPlanApiMock.upsertActionItemProgress).toHaveBeenCalledWith('diag-1', 'REC-ENV-01', {
      status: 'Done',
      assignedTo: 'Paul',
      dueDate: null,
      notes: null,
    })
    await screen.findByRole('button', { name: /Statut : Terminé/ })
  })

  // Un changement de statut n'emporte jamais une saisie en cours dans le formulaire.
  it('changer le statut n’envoie ni n’efface la saisie non enregistrée du formulaire', async () => {
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
      completedAt: null,
    })

    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Modifier les détails' }))
    await userEvent.type(screen.getByLabelText('Responsable'), 'Claire')
    await userEvent.click(screen.getByRole('button', { name: /Statut : Planifié/ }))
    await userEvent.click(await screen.findByRole('menuitemradio', { name: 'En cours' }))

    await waitFor(() =>
      expect(actionPlanApiMock.upsertActionItemProgress).toHaveBeenCalledWith('diag-1', 'REC-ENV-01', {
        status: 'InProgress',
        assignedTo: null,
        dueDate: null,
        notes: null,
      }),
    )
    await screen.findByRole('button', { name: /Statut : En cours/ })
    expect((screen.getByLabelText('Responsable') as HTMLInputElement).value).toBe('Claire')
    expect(screen.getByText('Modifications non enregistrées')).toBeDefined()
  })

  // Responsable, échéance et notes : un seul envoi, sur « Enregistrer », jamais à la frappe.
  it('responsable, échéance et notes s’enregistrent ensemble sur « Enregistrer »', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    actionPlanApiMock.getActionPlan.mockResolvedValue([
      makeActionItemWithProgress({ code: 'REC-ENV-01', status: 'InProgress', assignedTo: null, dueDate: null, notes: null }),
    ])
    actionPlanApiMock.upsertActionItemProgress.mockResolvedValue({
      diagnosticId: 'diag-1',
      code: 'REC-ENV-01',
      status: 'InProgress',
      assignedTo: 'Claire Martin',
      dueDate: '2026-11-15T00:00:00+00:00',
      notes: 'Devis signé',
      progressUpdatedAt: '2026-09-21T10:00:00Z',
      completedAt: null,
    })

    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Modifier les détails' }))

    const save = screen.getByRole('button', { name: 'Enregistrer' }) as HTMLButtonElement
    expect(save.disabled).toBe(true)

    await userEvent.type(screen.getByLabelText('Responsable'), 'Claire Martin')
    // Calendrier du site (components/ui/DatePicker.tsx) : sans date, il s'ouvre sur le mois en
    // cours ; on passe par la vue des années pour ne pas dépendre du jour où le test tourne.
    await userEvent.click(screen.getByLabelText('Échéance'))
    await userEvent.click(screen.getByRole('button', { name: /choisir l’année/ }))
    await userEvent.click(screen.getByRole('button', { name: '2026' }))
    const calendar = screen.getByRole('dialog', { name: 'Calendrier' })
    while (!within(calendar).queryByRole('grid', { name: 'novembre 2026' })) {
      const title = within(calendar).getByRole('button', { name: /choisir l’année/ }).textContent ?? ''
      const before = ['janvier', 'février', 'mars', 'avril', 'mai', 'juin', 'juillet', 'août', 'septembre', 'octobre'].some((m) => title.startsWith(m))
      await userEvent.click(within(calendar).getByRole('button', { name: before ? 'Mois suivant' : 'Mois précédent' }))
    }
    await userEvent.click(within(calendar).getByRole('button', { name: 'dimanche 15 novembre 2026' }))
    await userEvent.type(screen.getByLabelText('Notes de suivi'), 'Devis signé')

    expect(actionPlanApiMock.upsertActionItemProgress).not.toHaveBeenCalled()
    expect(screen.getByText('Modifications non enregistrées')).toBeDefined()

    await userEvent.click(save)

    await waitFor(() => expect(actionPlanApiMock.upsertActionItemProgress).toHaveBeenCalledOnce())
    expect(actionPlanApiMock.upsertActionItemProgress).toHaveBeenCalledWith('diag-1', 'REC-ENV-01', {
      status: 'InProgress',
      assignedTo: 'Claire Martin',
      dueDate: '2026-11-15',
      notes: 'Devis signé',
    })
    expect(await screen.findByText('Enregistré')).toBeDefined()
    expect((screen.getByRole('button', { name: 'Enregistrer' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('Annuler revient aux valeurs enregistrées, sans rien envoyer', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    actionPlanApiMock.getActionPlan.mockResolvedValue([makeActionItemWithProgress({ assignedTo: 'Paul' })])

    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Modifier les détails' }))
    await userEvent.clear(screen.getByLabelText('Responsable'))
    await userEvent.type(screen.getByLabelText('Responsable'), 'Claire')
    await userEvent.click(screen.getByRole('button', { name: 'Annuler' }))

    expect((screen.getByLabelText('Responsable') as HTMLInputElement).value).toBe('Paul')
    expect(screen.queryByRole('button', { name: 'Annuler' })).toBeNull()
    expect(actionPlanApiMock.upsertActionItemProgress).not.toHaveBeenCalled()
  })

  it('un échec d’enregistrement est signalé et la saisie conservée', async () => {
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    actionPlanApiMock.getActionPlan.mockResolvedValue([makeActionItemWithProgress({ assignedTo: null })])
    actionPlanApiMock.upsertActionItemProgress.mockRejectedValue(new Error('réseau'))

    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Modifier les détails' }))
    await userEvent.type(screen.getByLabelText('Responsable'), 'Claire')
    await userEvent.click(screen.getByRole('button', { name: 'Enregistrer' }))

    expect((await screen.findByRole('alert')).textContent).toMatch(/enregistrement a échoué/)
    expect((screen.getByLabelText('Responsable') as HTMLInputElement).value).toBe('Claire')
  })

  it('Viewer voit le statut sans pouvoir le changer, et le suivi en lecture', async () => {
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Viewer' }, error: null })
    dashboardApi.getDashboard.mockResolvedValue(
      makeDashboardView({ hasCompletedDiagnostic: true, latestDiagnostic: makeLatestDiagnostic({ id: 'diag-1' }) }),
    )
    actionPlanApiMock.getActionPlan.mockResolvedValue([makeActionItemWithProgress({ status: 'Planned', assignedTo: 'Paul' })])

    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Voir les détails' }))

    expect(screen.getByText('Planifié')).toBeDefined()
    expect(screen.queryByRole('button', { name: /^Statut/ })).toBeNull()
    expect(screen.getByText('Paul')).toBeDefined()
    expect(screen.queryByRole('button', { name: 'Enregistrer' })).toBeNull()
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
