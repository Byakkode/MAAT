import type { Entitlements, Subscription } from '../api/billingApi'

// Miroir des droits calculés par PlanEntitlements (backend/MAAT.Domain/Services/
// PlanEntitlements.cs, docs/specs/abonnement.md section 8), pour les tests d'écran.
export const STARTER_ENTITLEMENTS: Entitlements = {
  canStartDiagnostic: true,
  canViewDomainScores: false,
  visibleRecommendations: 3,
  canTrackActions: false,
  canEditActionPlan: false,
  canEditIndicators: false,
  canViewBenchmark: false,
  canOpenSupportTickets: false,
  fullReport: false,
  canCustomizeReportLogo: false,
  canViewActionHistory: false,
  canReadDocumentation: false,
}

export const ESSENTIAL_ENTITLEMENTS: Entitlements = {
  canStartDiagnostic: true,
  canViewDomainScores: true,
  visibleRecommendations: 12,
  canTrackActions: true,
  canEditActionPlan: false,
  // docs/specs/norme-volontaire.md : le rapport selon la norme volontaire en dépend.
  canEditIndicators: true,
  canViewBenchmark: false,
  canOpenSupportTickets: true,
  fullReport: true,
  canCustomizeReportLogo: true,
  canViewActionHistory: false,
  canReadDocumentation: true,
}

export const PROFESSIONAL_ENTITLEMENTS: Entitlements = {
  ...ESSENTIAL_ENTITLEMENTS,
  visibleRecommendations: null,
  canEditActionPlan: true,
  canViewBenchmark: true,
  canViewActionHistory: true,
}

export function makeSubscription(overrides: Partial<Subscription> = {}): Subscription {
  return {
    plan: 'Professional',
    billingPeriod: 'Monthly',
    status: 'Active',
    hasBillingAccount: true,
    effectivePlan: 'Professional',
    entitlements: PROFESSIONAL_ENTITLEMENTS,
    ...overrides,
  }
}
