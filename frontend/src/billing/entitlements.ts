import type { ApiPlan, Entitlements } from '../api/billingApi'
import { useSubscriptionStore } from '../store/subscriptionStore'

// Abonnement pas (encore) chargé ou en erreur : rien n'est masqué côté écran. L'API applique
// les limites de toute façon (docs/specs/abonnement.md, section 8) et répond 403 avec un
// message explicite, affiché là où l'action a été tentée ; masquer sur une panne de l'API de
// facturation ne protégerait rien.
const UNKNOWN: Entitlements = {
  canStartDiagnostic: true,
  canViewDomainScores: true,
  visibleRecommendations: null,
  canTrackActions: true,
  canEditActionPlan: true,
  canEditIndicators: true,
  canViewBenchmark: true,
  canOpenSupportTickets: true,
  fullReport: true,
  canCustomizeReportLogo: true,
  canViewActionHistory: true,
  canReadDocumentation: true,
}

export function useEntitlements(): Entitlements {
  return useSubscriptionStore((state) => state.subscription?.entitlements ?? UNKNOWN)
}

// Offre effective, pour le libellé « votre offre Starter ». null tant qu'elle est inconnue.
export function useEffectivePlan(): ApiPlan | null {
  return useSubscriptionStore((state) => state.subscription?.effectivePlan ?? null)
}
