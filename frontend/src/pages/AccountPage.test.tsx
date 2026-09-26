import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render as rtlRender, screen } from '@testing-library/react'
import type { ReactElement } from 'react'
import { MemoryRouter } from 'react-router-dom'

const accountApi = vi.hoisted(() => ({ getCurrentUser: vi.fn() }))
vi.mock('../api/accountApi', () => accountApi)
// SubscriptionCard (docs/specs/abonnement.md) relit l'abonnement au montage.
const billingApi = vi.hoisted(() => ({
  getSubscription: vi.fn(),
  refreshSubscription: vi.fn(),
  fromApiPlan: (plan: string) => plan.toLowerCase(),
}))
vi.mock('../api/billingApi', () => billingApi)

import { AccountPage } from './AccountPage'
import { useAuthStore } from '../store/authStore'
import { makeCurrentUser, resetCurrentUserStore } from '../test/currentUserFixtures'
import { useSubscriptionStore } from '../store/subscriptionStore'

// SubscriptionCard contient un <Link> vers l'écran de sélection : il exige un routeur.
function render(ui: ReactElement) {
  return rtlRender(<MemoryRouter>{ui}</MemoryRouter>)
}

describe('AccountPage', () => {
  beforeEach(() => {
    resetCurrentUserStore()
    useSubscriptionStore.setState({ status: 'idle', subscription: null })
    billingApi.getSubscription.mockReset().mockResolvedValue({ plan: 'Starter', billingPeriod: null, status: 'Active', hasBillingAccount: false })
    billingApi.refreshSubscription.mockReset().mockResolvedValue({ plan: 'Starter', billingPeriod: null, status: 'Active', hasBillingAccount: false })
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

  it('affiche les cinq blocs une fois le profil chargé', async () => {
    accountApi.getCurrentUser.mockResolvedValue(makeCurrentUser())

    render(<AccountPage />)

    expect(await screen.findByRole('heading', { name: 'Identité' })).toBeDefined()
    expect(screen.getByRole('heading', { name: 'Abonnement' })).toBeDefined()
    expect(await screen.findByText('Starter')).toBeDefined()
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
