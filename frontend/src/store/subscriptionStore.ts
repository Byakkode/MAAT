import { create } from 'zustand'
import * as billingApi from '../api/billingApi'
import type { Subscription } from '../api/billingApi'

export type SubscriptionLoadStatus = 'idle' | 'loading' | 'loaded' | 'error'

interface SubscriptionState {
  status: SubscriptionLoadStatus
  subscription: Subscription | null
  load: () => Promise<Subscription | null>
  set: (subscription: Subscription) => void
}

export const useSubscriptionStore = create<SubscriptionState>((set) => ({
  status: 'idle',
  subscription: null,

  async load() {
    set({ status: 'loading' })
    try {
      const subscription = await billingApi.getSubscription()
      set({ status: 'loaded', subscription })
      return subscription
    } catch {
      set({ status: 'error', subscription: null })
      return null
    }
  },

  set(subscription) {
    set({ status: 'loaded', subscription })
  },
}))

// docs/specs/abonnement.md, section 2 : l'application n'est accessible qu'une fois une offre
// choisie — Starter, ou une offre payante dont le paiement a été reçu.
export function needsPlanChoice(subscription: Subscription | null): boolean {
  return subscription !== null && (subscription.status === null || subscription.status === 'PendingPayment')
}
