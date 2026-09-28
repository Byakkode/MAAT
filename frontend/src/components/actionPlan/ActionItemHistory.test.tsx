import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from 'vitest-axe'
import type { ActionItemChange } from '../../api/actionPlanApi'

const actionPlanApi = vi.hoisted(() => ({ getActionItemHistory: vi.fn() }))
vi.mock('../../api/actionPlanApi', () => actionPlanApi)

import { ActionItemHistory } from './ActionItemHistory'
import { describeChange } from './describeChange'

const change = (overrides: Partial<ActionItemChange>): ActionItemChange => ({
  field: 'Status',
  oldValue: 'Planned',
  newValue: 'InProgress',
  changedAt: '2026-10-12T12:05:00Z',
  changedBy: 'claire@entreprise.test',
  ...overrides,
})

// docs/specs/recommandations.md, section 4 bis.
describe('ActionItemHistory', () => {
  afterEach(() => {
    actionPlanApi.getActionItemHistory.mockReset()
  })

  it('décrit chaque modification en français, notes sans leur contenu', () => {
    expect(describeChange(change({}))).toBe('Statut : Planifié → En cours')
    expect(describeChange(change({ field: 'AssignedTo', oldValue: null, newValue: 'Claire Martin' }))).toBe('Responsable : — → Claire Martin')
    expect(describeChange(change({ field: 'DueDate', oldValue: '2026-11-15', newValue: null }))).toBe('Échéance : 15 novembre 2026 → —')
    expect(describeChange(change({ field: 'Notes', oldValue: null, newValue: null }))).toBe('Notes modifiées')
  })

  it('fermé par défaut : aucun appel réseau tant qu’on ne l’ouvre pas', () => {
    render(<ActionItemHistory diagnosticId="d-1" code="ENV-01" refreshKey={null} />)

    expect(screen.getByRole('button', { name: "Voir l'historique" }).getAttribute('aria-expanded')).toBe('false')
    expect(actionPlanApi.getActionItemHistory).not.toHaveBeenCalled()
  })

  it('à l’ouverture, liste les modifications avec leur auteur, compte supprimé compris', async () => {
    actionPlanApi.getActionItemHistory.mockResolvedValue([
      change({}),
      change({ field: 'Notes', oldValue: null, newValue: null, changedBy: null }),
    ])

    render(<ActionItemHistory diagnosticId="d-1" code="ENV-01" refreshKey={null} />)
    await userEvent.click(screen.getByRole('button', { name: "Voir l'historique" }))

    const list = await screen.findByRole('list', { name: 'Historique du suivi' })
    const items = within(list).getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(items[0]!.textContent).toMatch(/Statut : Planifié → En cours/)
    expect(items[0]!.textContent).toMatch(/claire@entreprise\.test/)
    expect(items[1]!.textContent).toMatch(/Notes modifiées/)
    expect(items[1]!.textContent).toMatch(/Compte supprimé/)
    expect(actionPlanApi.getActionItemHistory).toHaveBeenCalledWith('d-1', 'ENV-01')
  })

  it('sans modification : un message explicite, pas une liste vide', async () => {
    actionPlanApi.getActionItemHistory.mockResolvedValue([])

    render(<ActionItemHistory diagnosticId="d-1" code="ENV-01" refreshKey={null} />)
    await userEvent.click(screen.getByRole('button', { name: "Voir l'historique" }))

    expect(await screen.findByText('Aucune modification enregistrée pour cette action.')).toBeDefined()
  })

  it('signale une erreur de chargement', async () => {
    actionPlanApi.getActionItemHistory.mockRejectedValue(new Error('réseau'))

    render(<ActionItemHistory diagnosticId="d-1" code="ENV-01" refreshKey={null} />)
    await userEvent.click(screen.getByRole('button', { name: "Voir l'historique" }))

    expect((await screen.findByRole('alert')).textContent).toMatch(/Impossible de charger/)
  })

  it('ouvert, se recharge après un enregistrement du suivi', async () => {
    actionPlanApi.getActionItemHistory.mockResolvedValueOnce([change({})]).mockResolvedValueOnce([
      change({ oldValue: 'InProgress', newValue: 'Done', changedAt: '2026-10-12T12:30:00Z' }),
      change({}),
    ])

    const { rerender } = render(<ActionItemHistory diagnosticId="d-1" code="ENV-01" refreshKey="t1" />)
    await userEvent.click(screen.getByRole('button', { name: "Voir l'historique" }))
    expect(await screen.findAllByRole('listitem')).toHaveLength(1)

    rerender(<ActionItemHistory diagnosticId="d-1" code="ENV-01" refreshKey="t2" />)

    expect(await screen.findByText('Statut : En cours → Terminé')).toBeDefined()
    expect(actionPlanApi.getActionItemHistory).toHaveBeenCalledTimes(2)
  })

  it('n’a pas de violation d’accessibilité détectable', async () => {
    actionPlanApi.getActionItemHistory.mockResolvedValue([change({})])

    const { container } = render(<ActionItemHistory diagnosticId="d-1" code="ENV-01" refreshKey={null} />)
    await userEvent.click(screen.getByRole('button', { name: "Voir l'historique" }))
    await screen.findByRole('list', { name: 'Historique du suivi' })

    expect(await axe(container)).toHaveNoViolations()
  })
})
