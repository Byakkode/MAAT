import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from 'vitest-axe'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

const billingApi = vi.hoisted(() => ({
  getSubscription: vi.fn(),
  chooseStarter: vi.fn(),
  startCheckout: vi.fn(),
  openCustomerPortal: vi.fn(),
  redirectTo: vi.fn(),
  fromApiPlan: (plan: string) => plan.toLowerCase(),
}))
vi.mock('../api/billingApi', () => billingApi)

import { SubscriptionPage } from './SubscriptionPage'
import { useAuthStore } from '../store/authStore'
import { useSubscriptionStore } from '../store/subscriptionStore'
import type { Subscription } from '../api/billingApi'

const NO_PLAN: Subscription = { plan: null, billingPeriod: null, status: null, hasBillingAccount: false }

function renderPage(url = '/abonnement') {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/abonnement" element={<SubscriptionPage />} />
        <Route path="/tableau-de-bord" element={<p>Tableau de bord</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

function planColumn(name: RegExp) {
  return screen.getByRole('columnheader', { name })
}

describe('SubscriptionPage', () => {
  beforeEach(() => {
    for (const fn of [billingApi.getSubscription, billingApi.chooseStarter, billingApi.startCheckout, billingApi.openCustomerPortal, billingApi.redirectTo]) {
      fn.mockReset()
    }
    useSubscriptionStore.setState({ status: 'idle', subscription: null })
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role: 'Admin' }, error: null })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  // Délai porté à 15 s : axe-core parcourt chaque élément du DOM, et le comparatif des offres
  // (4 offres × 25 options, docs/specs/abonnement.md) le rend assez lourd pour dépasser les
  // 5 s par défaut quand toute la suite tourne en parallèle.
  it('ne signale aucune violation d’accessibilité détectable', async () => {
    billingApi.getSubscription.mockResolvedValue(NO_PLAN)
    const { container } = renderPage()
    await screen.findByRole('heading', { name: 'Choisissez votre offre' })

    expect(await axe(container)).toHaveNoViolations()
  }, 15_000)

  it('active Starter et ouvre le tableau de bord', async () => {
    const user = userEvent.setup()
    billingApi.getSubscription.mockResolvedValue(NO_PLAN)
    billingApi.chooseStarter.mockResolvedValue({ plan: 'Starter', billingPeriod: null, status: 'Active', hasBillingAccount: false })
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Commencer gratuitement' }))

    expect(await screen.findByText('Tableau de bord')).toBeTruthy()
    expect(useSubscriptionStore.getState().subscription?.plan).toBe('Starter')
  })

  it('envoie vers le paiement Stripe de l’offre et de la période choisies', async () => {
    const user = userEvent.setup()
    billingApi.getSubscription.mockResolvedValue(NO_PLAN)
    billingApi.startCheckout.mockResolvedValue('https://checkout.stripe.test/s')
    renderPage()

    await user.click(await screen.findByRole('radio', { name: /annuel/i }))
    await user.click(screen.getByRole('button', { name: 'Choisir Professional' }))

    expect(billingApi.startCheckout).toHaveBeenCalledWith('professional', 'yearly')
    await waitFor(() => expect(billingApi.redirectTo).toHaveBeenCalledWith('https://checkout.stripe.test/s'))
  })

  it('ne propose aucune action pour Enterprise', async () => {
    billingApi.getSubscription.mockResolvedValue(NO_PLAN)
    renderPage()
    await screen.findByRole('heading', { name: 'Choisissez votre offre' })

    const enterprise = planColumn(/enterprise/i)
    expect(within(enterprise).queryByRole('button')).toBeNull()
    expect(within(enterprise).getAllByText('Bientôt disponible').length).toBeGreaterThan(0)
  })

  // Offre payante choisie sur la page d'accueil : directement vers le paiement, sans
  // repasser par la sélection (docs/specs/abonnement.md, section 2).
  it('reprend directement le paiement de l’offre choisie à l’inscription', async () => {
    billingApi.getSubscription.mockResolvedValue({ plan: 'Essential', billingPeriod: 'Yearly', status: 'PendingPayment', hasBillingAccount: false })
    billingApi.startCheckout.mockResolvedValue('https://checkout.stripe.test/s')
    renderPage()

    expect((await screen.findByRole('status')).textContent).toMatch(/redirection vers le paiement sécurisé de l'offre essential/i)
    await waitFor(() => expect(billingApi.redirectTo).toHaveBeenCalledWith('https://checkout.stripe.test/s'))
    expect(billingApi.startCheckout).toHaveBeenCalledTimes(1)
    expect(billingApi.startCheckout).toHaveBeenCalledWith('essential', 'yearly')
  })

  it('après un paiement annulé, affiche la sélection au lieu de relancer le paiement', async () => {
    billingApi.getSubscription.mockResolvedValue({ plan: 'Essential', billingPeriod: 'Monthly', status: 'PendingPayment', hasBillingAccount: false })
    renderPage('/abonnement?paiement=annule')

    expect(await screen.findByText(/paiement annulé/i)).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Choisir Essential' })).toBeTruthy()
    expect(billingApi.startCheckout).not.toHaveBeenCalled()
  })

  it('affiche l’erreur quand le paiement ne peut pas être ouvert', async () => {
    const user = userEvent.setup()
    billingApi.getSubscription.mockResolvedValue(NO_PLAN)
    billingApi.startCheckout.mockRejectedValue(new Error('Le paiement en ligne est temporairement indisponible.'))
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Choisir Essential' }))

    expect((await screen.findByRole('alert')).textContent).toBe('Le paiement en ligne est temporairement indisponible.')
    expect(billingApi.redirectTo).not.toHaveBeenCalled()
  })

  it('un abonnement payant en cours se modifie depuis le portail Stripe', async () => {
    const user = userEvent.setup()
    billingApi.getSubscription.mockResolvedValue({ plan: 'Essential', billingPeriod: 'Monthly', status: 'Active', hasBillingAccount: true })
    billingApi.openCustomerPortal.mockResolvedValue('https://billing.stripe.test/p')
    renderPage()

    await screen.findByRole('heading', { name: 'Votre abonnement' })
    expect(within(planColumn(/essential/i)).getByText('Offre actuelle')).toBeTruthy()
    await user.click(within(planColumn(/professional/i)).getByRole('button', { name: "Changer d'offre" }))

    await waitFor(() => expect(billingApi.redirectTo).toHaveBeenCalledWith('https://billing.stripe.test/p'))
    expect(billingApi.startCheckout).not.toHaveBeenCalled()
  })

  it('un membre non administrateur voit les offres sans pouvoir en choisir', async () => {
    useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-2', companyId: 'c-1', role: 'Viewer' }, error: null })
    billingApi.getSubscription.mockResolvedValue({ plan: 'Essential', billingPeriod: 'Monthly', status: 'PendingPayment', hasBillingAccount: false })
    renderPage()

    expect(await screen.findByText(/seul un administrateur/i)).toBeTruthy()
    expect(screen.queryByRole('button', { name: /choisir|commencer/i })).toBeNull()
    expect(billingApi.startCheckout).not.toHaveBeenCalled()
  })
})
