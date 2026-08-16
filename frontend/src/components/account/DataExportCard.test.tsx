import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

const accountApi = vi.hoisted(() => ({ exportData: vi.fn() }))
vi.mock('../../api/accountApi', () => accountApi)

import { DataExportCard } from './DataExportCard'

// docs/specs/coquille-et-compte.md, cas de test 16 et 17.
describe('DataExportCard', () => {
  beforeEach(() => {
    accountApi.exportData.mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('cas 16 : demande le mot de passe puis déclenche l’export', async () => {
    accountApi.exportData.mockResolvedValue(undefined)

    render(<DataExportCard />)
    await userEvent.type(screen.getByLabelText('Mot de passe'), 'MotDePasseValide2026!')
    await userEvent.click(screen.getByRole('button', { name: /exporter mes données/i }))

    await screen.findByText('Export téléchargé.')
    expect(accountApi.exportData).toHaveBeenCalledWith('MotDePasseValide2026!')
  })

  // cas 17 : le formulaire ne porte aucune condition de rôle — l'export est un droit
  // personnel de tout principal authentifié (Viewer compris), pas une action d'administration
  // désactivée pour certains rôles comme le sont les actions du plan d'actions.
  it('cas 17 : le bouton d’export n’est jamais désactivé selon le rôle', () => {
    render(<DataExportCard />)

    expect(screen.getByRole('button', { name: /exporter mes données/i }).hasAttribute('disabled')).toBe(false)
  })
})
