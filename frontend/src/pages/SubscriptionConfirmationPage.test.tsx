import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

const billingApi = vi.hoisted(() => ({ getSubscription: vi.fn(), confirmCheckout: vi.fn() }))
vi.mock('../api/billingApi', () => billingApi)

import { SubscriptionConfirmationPage } from './SubscriptionConfirmationPage'
import { useSubscriptionStore } from '../store/subscriptionStore'

const PENDING = { plan: 'Essential', billingPeriod: 'Monthly', status: 'PendingPayment', hasBillingAccount: false }
const ACTIVE = { plan: 'Essential', billingPeriod: 'Monthly', status: 'Active', hasBillingAccount: true }

function renderPage(url = '/abonnement/confirmation?session_id=cs_test_123') {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/abonnement/confirmation" element={<SubscriptionConfirmationPage />} />
        <Route path="/tableau-de-bord" element={<p>Tableau de bord</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

// docs/specs/abonnement.md, section 5 : au retour de Stripe, l'API relit la session et active
// l'offre ; le tableau de bord s'ouvre sans action de l'utilisateur. Le webhook n'est qu'un
// repli.
describe('SubscriptionConfirmationPage', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    billingApi.getSubscription.mockReset()
    billingApi.confirmCheckout.mockReset()
    useSubscriptionStore.setState({ status: 'idle', subscription: null })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('confirme la session auprès de l’API puis ouvre le tableau de bord, sans attendre le webhook', async () => {
    billingApi.confirmCheckout.mockResolvedValue(ACTIVE)
    renderPage()

    await act(() => vi.advanceTimersByTimeAsync(0))
    expect(billingApi.confirmCheckout).toHaveBeenCalledWith('cs_test_123')
    expect(screen.getByRole('status').textContent).toMatch(/votre abonnement est actif/i)
    expect(billingApi.getSubscription).not.toHaveBeenCalled()

    await act(() => vi.advanceTimersByTimeAsync(1_200))
    expect(screen.getByText('Tableau de bord')).toBeTruthy()
  })

  it('session pas encore payée : attend l’activation par le webhook', async () => {
    billingApi.confirmCheckout.mockResolvedValue(PENDING)
    billingApi.getSubscription.mockResolvedValueOnce(PENDING).mockResolvedValue(ACTIVE)
    renderPage()

    await act(() => vi.advanceTimersByTimeAsync(0))
    expect(screen.getByRole('status').textContent).toMatch(/vérification de votre paiement/i)

    await act(() => vi.advanceTimersByTimeAsync(2_000))
    expect(screen.getByRole('status').textContent).toMatch(/votre abonnement est actif/i)

    await act(() => vi.advanceTimersByTimeAsync(1_200))
    expect(screen.getByText('Tableau de bord')).toBeTruthy()
  })

  it('confirmation impossible : se replie sur l’attente du webhook', async () => {
    billingApi.confirmCheckout.mockRejectedValue(new Error('Stripe injoignable'))
    billingApi.getSubscription.mockResolvedValue(ACTIVE)
    renderPage()

    await act(() => vi.advanceTimersByTimeAsync(0))

    expect(screen.getByRole('status').textContent).toMatch(/votre abonnement est actif/i)
  })

  it('sans confirmation au bout de 30 s, rassure sans proposer de payer à nouveau', async () => {
    billingApi.confirmCheckout.mockResolvedValue(PENDING)
    billingApi.getSubscription.mockResolvedValue(PENDING)
    renderPage()

    await act(() => vi.advanceTimersByTimeAsync(31_000))

    expect(screen.getByRole('status').textContent).toMatch(/aucun second paiement/i)
    expect(screen.getByRole('button', { name: 'Vérifier à nouveau' })).toBeTruthy()
    expect(screen.queryByText('Tableau de bord')).toBeNull()
  })
})
