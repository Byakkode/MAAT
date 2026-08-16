import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'

const accountApi = vi.hoisted(() => ({ getCurrentUser: vi.fn() }))
vi.mock('../api/accountApi', () => accountApi)

import { AccountPage } from './AccountPage'
import { useAuthStore } from '../store/authStore'
import { makeCurrentUser, resetCurrentUserStore } from '../test/currentUserFixtures'

describe('AccountPage', () => {
  beforeEach(() => {
    resetCurrentUserStore()
    accountApi.getCurrentUser.mockReset()
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Admin' }, error: null })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('affiche un état de chargement avant que le profil soit disponible', () => {
    accountApi.getCurrentUser.mockReturnValue(new Promise(() => {}))

    render(<AccountPage />)

    expect(screen.getByRole('status').textContent).toBe('Chargement de votre compte…')
  })

  it('affiche les quatre blocs une fois le profil chargé', async () => {
    accountApi.getCurrentUser.mockResolvedValue(makeCurrentUser())

    render(<AccountPage />)

    expect(await screen.findByRole('heading', { name: 'Identité' })).toBeDefined()
    expect(screen.getByRole('heading', { name: 'Mot de passe' })).toBeDefined()
    expect(screen.getByRole('heading', { name: 'Mes données' })).toBeDefined()
    expect(screen.getByRole('heading', { name: 'Suppression du compte' })).toBeDefined()
  })

  it('affiche une erreur si le profil ne peut pas être récupéré', async () => {
    accountApi.getCurrentUser.mockRejectedValue(new Error('erreur réseau'))

    render(<AccountPage />)

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toBe('Impossible de récupérer votre compte.')
  })
})
