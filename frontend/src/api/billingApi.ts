import { apiFetch } from './httpClient'
import { ApiError } from './authApi'
import type { BillingPeriodChoice, PlanId } from '../billing/plans'

// Miroir de MAAT.Application.DTOs.SubscriptionView (backend/MAAT.Application/DTOs/SubscriptionView.cs).
// Énumérations sérialisées en chaîne par l'API (JsonStringEnumConverter).
export type ApiPlan = 'Starter' | 'Essential' | 'Professional' | 'Enterprise'
export type ApiBillingPeriod = 'Monthly' | 'Yearly'
export type SubscriptionStatus = 'PendingPayment' | 'Active' | 'PastDue'

export interface Subscription {
  plan: ApiPlan | null
  billingPeriod: ApiBillingPeriod | null
  // null : l'entreprise n'a pas encore choisi d'offre (docs/specs/abonnement.md, section 2).
  status: SubscriptionStatus | null
  hasBillingAccount: boolean
}

const API_PLANS: Record<PlanId, ApiPlan> = {
  starter: 'Starter',
  essential: 'Essential',
  professional: 'Professional',
  enterprise: 'Enterprise',
}

export function toApiPlan(plan: PlanId): ApiPlan {
  return API_PLANS[plan]
}

export function toApiPeriod(period: BillingPeriodChoice): ApiBillingPeriod {
  return period === 'monthly' ? 'Monthly' : 'Yearly'
}

export function fromApiPlan(plan: ApiPlan): PlanId {
  return plan.toLowerCase() as PlanId
}

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  const body = await response.json().catch(() => null)
  return (body as { message?: string } | null)?.message ?? fallback
}

export async function getSubscription(): Promise<Subscription> {
  const response = await apiFetch('/api/billing/subscription')
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, "Impossible de charger l'abonnement."), response.status)
  }
  return (await response.json()) as Subscription
}

export async function chooseStarter(): Promise<Subscription> {
  const response = await apiFetch('/api/billing/starter', { method: 'POST' })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, "Impossible de choisir l'offre Starter."), response.status)
  }
  return (await response.json()) as Subscription
}

// Renvoie l'URL de la page de paiement hébergée par Stripe, vers laquelle rediriger.
export async function startCheckout(plan: PlanId, period: BillingPeriodChoice): Promise<string> {
  const response = await apiFetch('/api/billing/checkout', {
    method: 'POST',
    body: JSON.stringify({ plan: toApiPlan(plan), billingPeriod: toApiPeriod(period) }),
  })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, "Impossible d'ouvrir la page de paiement."), response.status)
  }
  return ((await response.json()) as { url: string }).url
}

// Retour de la page de paiement : l'API relit la session chez Stripe et active l'offre si elle
// est payée, sans attendre le webhook (docs/specs/abonnement.md, section 5).
export async function confirmCheckout(sessionId: string): Promise<Subscription> {
  const response = await apiFetch('/api/billing/checkout/confirm', {
    method: 'POST',
    body: JSON.stringify({ sessionId }),
  })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de confirmer le paiement.'), response.status)
  }
  return (await response.json()) as Subscription
}

// Retour du portail client : l'API relit l'abonnement chez Stripe (changement d'offre,
// résiliation) avant de le renvoyer.
export async function refreshSubscription(): Promise<Subscription> {
  const response = await apiFetch('/api/billing/refresh', { method: 'POST' })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, "Impossible de charger l'abonnement."), response.status)
  }
  return (await response.json()) as Subscription
}

// Renvoie l'URL du portail client Stripe (changement d'offre, factures, résiliation).
export async function openCustomerPortal(): Promise<string> {
  const response = await apiFetch('/api/billing/portal', { method: 'POST' })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, "Impossible d'ouvrir la gestion de l'abonnement."), response.status)
  }
  return ((await response.json()) as { url: string }).url
}

// Point de sortie unique vers Stripe : isolé ici pour que les tests le remplacent sans toucher
// à window.location, que jsdom ne sait pas faire naviguer.
export function redirectTo(url: string): void {
  window.location.assign(url)
}
