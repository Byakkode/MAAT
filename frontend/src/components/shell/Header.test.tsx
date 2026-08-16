import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

const authApi = vi.hoisted(() => ({ logout: vi.fn() }))
vi.mock('../../api/authApi', () => authApi)

import { Header } from './Header'
import { ProtectedRoute } from '../ProtectedRoute'
import { useAuthStore } from '../../store/authStore'
import { getAccessToken, setAccessToken } from '../../api/tokenStore'
import { makeCurrentUser, resetCurrentUserStore } from '../../test/currentUserFixtures'
import { useCurrentUserStore } from '../../store/currentUserStore'

function renderHeaderInProtectedShell() {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route path="/login" element={<p>Page de connexion</p>} />
        <Route element={<ProtectedRoute />}>
          <Route path="/" element={<Header onOpenMobileNav={() => {}} />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

// docs/specs/coquille-et-compte.md, cas de test 9 et 12 (10 et 11 : voir VerificationBanner.test.tsx).
describe('Header', () => {
  beforeEach(() => {
    resetCurrentUserStore()
    setAccessToken('un-jeton-en-memoire')
    // Le mock reproduit l'effet de bord réel d'authApi.logout (voir authApi.ts) : effacer le
    // jeton en mémoire fait partie de ce que le cas 12 vérifie, pas seulement l'appel réseau.
    authApi.logout.mockReset().mockImplementation(async () => {
      setAccessToken(null)
    })
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Admin' }, error: null })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('cas 9 : le rôle du principal authentifié est affiché en toutes lettres', () => {
    useCurrentUserStore.setState({ status: 'loaded', ...makeCurrentUser({ role: 'Admin' }) })

    renderHeaderInProtectedShell()

    expect(screen.getByText('Administrateur')).toBeDefined()
    expect(screen.queryByText('Admin')).toBeNull()
  })

  it('cas 12 : la déconnexion efface le jeton de la mémoire et redirige vers la connexion', async () => {
    useCurrentUserStore.setState({ status: 'loaded', ...makeCurrentUser() })

    renderHeaderInProtectedShell()

    await userEvent.click(screen.getByRole('button', { name: /déconnexion/i }))

    await waitFor(() => expect(screen.getByText('Page de connexion')).toBeDefined())
    expect(authApi.logout).toHaveBeenCalled()
    expect(getAccessToken()).toBeNull()
  })
})
