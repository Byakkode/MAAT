import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

const billingApi = vi.hoisted(() => ({ getSubscription: vi.fn() }))
vi.mock('../api/billingApi', () => billingApi)

import { SubscriptionGate } from './SubscriptionGate'
import { useSubscriptionStore } from '../store/subscriptionStore'

function renderGate() {
  return render(
    <MemoryRouter initialEntries={['/tableau-de-bord']}>
      <Routes>
        <Route element={<SubscriptionGate />}>
          <Route path="/tableau-de-bord" element={<p>Tableau de bord</p>} />
        </Route>
        <Route path="/abonnement" element={<p>Sélection de l’offre</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

// docs/specs/abonnement.md, section 2.
describe('SubscriptionGate', () => {
  beforeEach(() => {
    billingApi.getSubscription.mockReset()
    useSubscriptionStore.setState({ status: 'idle', subscription: null })
  })

  it('envoie une entreprise sans offre sur l’écran de sélection', async () => {
    billingApi.getSubscription.mockResolvedValue({ plan: null, billingPeriod: null, status: null, hasBillingAccount: false })
    renderGate()

    expect(await screen.findByText('Sélection de l’offre')).toBeTruthy()
  })

  it('envoie une offre payante non réglée sur l’écran de sélection', async () => {
    billingApi.getSubscription.mockResolvedValue({ plan: 'Essential', billingPeriod: 'Monthly', status: 'PendingPayment', hasBillingAccount: false })
    renderGate()

    expect(await screen.findByText('Sélection de l’offre')).toBeTruthy()
  })

  it.each(['Active', 'PastDue'] as const)('laisse passer une offre au statut %s', async (status) => {
    billingApi.getSubscription.mockResolvedValue({ plan: 'Starter', billingPeriod: null, status, hasBillingAccount: false })
    renderGate()

    expect(await screen.findByText('Tableau de bord')).toBeTruthy()
  })

  // Aucune fonctionnalité ne dépend encore de l'offre : une panne de l'API de facturation ne
  // doit pas fermer l'application.
  it('laisse passer quand l’abonnement ne peut pas être chargé', async () => {
    billingApi.getSubscription.mockRejectedValue(new Error('réseau'))
    renderGate()

    expect(await screen.findByText('Tableau de bord')).toBeTruthy()
  })

  // Un état « chargé » laissé par l'écran précédent ne doit pas afficher la coquille avant
  // le rechargement (elle serait démontée aussitôt).
  it('attend son propre chargement avant de décider', () => {
    useSubscriptionStore.setState({ status: 'loaded', subscription: { plan: 'Starter', billingPeriod: null, status: 'Active', hasBillingAccount: false } })
    billingApi.getSubscription.mockReturnValue(new Promise(() => {}))
    renderGate()

    expect(screen.getByRole('status').textContent).toBe('Chargement de votre abonnement…')
    expect(screen.queryByText('Tableau de bord')).toBeNull()
  })
})
