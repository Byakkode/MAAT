import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

const authApi = vi.hoisted(() => ({ resendVerification: vi.fn() }))
vi.mock('../../api/authApi', () => authApi)

import { VerificationBanner } from './VerificationBanner'
import { useCurrentUserStore } from '../../store/currentUserStore'
import { makeCurrentUser, resetCurrentUserStore } from '../../test/currentUserFixtures'

// docs/specs/coquille-et-compte.md, cas de test 10 et 11.
describe('VerificationBanner', () => {
  beforeEach(() => {
    resetCurrentUserStore()
    authApi.resendVerification.mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('cas 10 : adresse non vérifiée → bandeau présent, bouton de renvoi fonctionnel', async () => {
    authApi.resendVerification.mockResolvedValue({ message: 'ok' })
    useCurrentUserStore.setState({ status: 'loaded', ...makeCurrentUser({ emailVerified: false }) })

    render(<VerificationBanner />)

    expect(screen.getByRole('status')).toBeDefined()

    await userEvent.click(screen.getByRole('button', { name: /renvoyer/i }))

    await waitFor(() => expect(authApi.resendVerification).toHaveBeenCalledWith('admin@entreprise.test'))
    await screen.findByText(/vient d’être envoyé/i)
  })

  it('cas 11 : adresse vérifiée → aucun bandeau', () => {
    useCurrentUserStore.setState({ status: 'loaded', ...makeCurrentUser({ emailVerified: true }) })

    render(<VerificationBanner />)

    expect(screen.queryByRole('status')).toBeNull()
  })
})
