import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

const accountApi = vi.hoisted(() => ({ changePassword: vi.fn() }))
vi.mock('../../api/accountApi', () => accountApi)

import { PasswordChangeCard } from './PasswordChangeCard'
import { ApiError } from '../../api/authApi'

// docs/specs/coquille-et-compte.md, cas de test 14 et 15.
describe('PasswordChangeCard', () => {
  beforeEach(() => {
    accountApi.changePassword.mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('avertit avant validation que les autres sessions seront invalidées', () => {
    render(<PasswordChangeCard />)

    expect(screen.getByText(/déconnecte toutes vos autres sessions/i)).toBeDefined()
  })

  it('cas 14 : changement sans le mot de passe actuel valide → refusé par le serveur, affiché', async () => {
    accountApi.changePassword.mockRejectedValue(new ApiError('Mot de passe actuel incorrect.', 401))

    render(<PasswordChangeCard />)
    await userEvent.type(screen.getByLabelText('Nouveau mot de passe'), 'NouveauMotDePasse2026!')
    await userEvent.click(screen.getByRole('button', { name: /modifier le mot de passe/i }))

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toBe('Mot de passe actuel incorrect.')
    expect(accountApi.changePassword).toHaveBeenCalledWith('', 'NouveauMotDePasse2026!')
  })

  it('cas 15 : changement réussi affiche une confirmation', async () => {
    accountApi.changePassword.mockResolvedValue(undefined)

    render(<PasswordChangeCard />)
    await userEvent.type(screen.getByLabelText('Mot de passe actuel'), 'AncienMotDePasse2026!')
    await userEvent.type(screen.getByLabelText('Nouveau mot de passe'), 'NouveauMotDePasse2026!')
    await userEvent.click(screen.getByRole('button', { name: /modifier le mot de passe/i }))

    await screen.findByText('Mot de passe modifié.')
    expect(accountApi.changePassword).toHaveBeenCalledWith('AncienMotDePasse2026!', 'NouveauMotDePasse2026!')
  })
})
