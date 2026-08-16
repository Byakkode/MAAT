import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { IdentityCard } from './IdentityCard'
import { useAuthStore } from '../../store/authStore'
import { useCurrentUserStore } from '../../store/currentUserStore'
import { makeCurrentUser, resetCurrentUserStore } from '../../test/currentUserFixtures'

// docs/specs/coquille-et-compte.md, cas de test 13.
describe('IdentityCard', () => {
  beforeEach(() => {
    resetCurrentUserStore()
  })

  afterEach(() => {
    useAuthStore.setState({ status: 'authenticated', user: null, error: null })
  })

  it('cas 13 : affiche les informations du principal authentifié, jamais d’un autre compte', () => {
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'User' }, error: null })
    useCurrentUserStore.setState({
      status: 'loaded',
      ...makeCurrentUser({
        email: 'salarie@entreprise-a.test',
        companyName: 'Entreprise A',
        role: 'User',
        createdAt: '2026-02-01T10:00:00Z',
      }),
    })

    render(<IdentityCard />)

    expect(screen.getByText('salarie@entreprise-a.test')).toBeDefined()
    expect(screen.getByText('Utilisateur')).toBeDefined()
    expect(screen.getByText('Entreprise A')).toBeDefined()
    expect(screen.getByText(/1 février 2026/)).toBeDefined()
    expect(screen.queryByText('Entreprise B')).toBeNull()
  })
})
