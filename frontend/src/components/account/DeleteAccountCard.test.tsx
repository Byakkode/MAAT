import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

const accountApi = vi.hoisted(() => ({ deleteAccount: vi.fn() }))
vi.mock('../../api/accountApi', () => accountApi)

const tokenStore = vi.hoisted(() => ({ notifySessionExpired: vi.fn() }))
vi.mock('../../api/tokenStore', () => tokenStore)

import { DeleteAccountCard } from './DeleteAccountCard'
import { useCurrentUserStore } from '../../store/currentUserStore'
import { makeCurrentUser, resetCurrentUserStore } from '../../test/currentUserFixtures'

// docs/specs/coquille-et-compte.md, cas de test 18.
describe('DeleteAccountCard', () => {
  beforeEach(() => {
    resetCurrentUserStore()
    useCurrentUserStore.setState({ status: 'loaded', ...makeCurrentUser({ email: 'admin@entreprise.test' }) })
    accountApi.deleteAccount.mockReset()
    tokenStore.notifySessionExpired.mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('cas 18 : confirmation par une adresse e-mail incorrecte → le bouton reste désactivé', async () => {
    render(<DeleteAccountCard />)

    await userEvent.type(screen.getByLabelText(/saisissez votre adresse e-mail/i), 'mauvaise-adresse@ailleurs.test')
    await userEvent.type(screen.getByLabelText('Mot de passe'), 'MotDePasseValide2026!')

    const submit = screen.getByRole('button', { name: /supprimer définitivement/i })
    expect(submit.hasAttribute('disabled')).toBe(true)

    await userEvent.click(submit)
    expect(accountApi.deleteAccount).not.toHaveBeenCalled()
  })

  it('adresse e-mail correcte → le bouton s’active et déclenche la suppression', async () => {
    accountApi.deleteAccount.mockResolvedValue(undefined)

    render(<DeleteAccountCard />)

    await userEvent.type(screen.getByLabelText(/saisissez votre adresse e-mail/i), 'admin@entreprise.test')
    await userEvent.type(screen.getByLabelText('Mot de passe'), 'MotDePasseValide2026!')

    const submit = screen.getByRole('button', { name: /supprimer définitivement/i })
    expect(submit.hasAttribute('disabled')).toBe(false)

    await userEvent.click(submit)

    expect(accountApi.deleteAccount).toHaveBeenCalledWith('MotDePasseValide2026!')
    expect(tokenStore.notifySessionExpired).toHaveBeenCalled()
  })

  it('dernier Admin : énonce que l’entreprise entière disparaît, avant la saisie', () => {
    useCurrentUserStore.setState({ isLastAdmin: true })

    render(<DeleteAccountCard />)

    expect(screen.getByText(/tous ses comptes utilisateurs, pas seulement le vôtre/i)).toBeDefined()
    expect(screen.getByRole('button', { name: 'Supprimer définitivement mon compte et mon entreprise' })).toBeDefined()
  })

  it('pas le dernier Admin : énonce que seul le compte disparaît, l’entreprise reste', () => {
    useCurrentUserStore.setState({ isLastAdmin: false })

    render(<DeleteAccountCard />)

    expect(screen.getByText(/ne supprime que votre propre compte/i)).toBeDefined()
    expect(screen.queryByText(/tous ses comptes utilisateurs/i)).toBeNull()
    expect(screen.getByRole('button', { name: 'Supprimer définitivement mon compte' })).toBeDefined()
  })
})
