import { useCurrentUserStore } from '../store/currentUserStore'
import type { CurrentUser } from '../api/accountApi'

export function makeCurrentUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    userId: 'u-1',
    companyId: 'c-1',
    companyName: 'Entreprise Test',
    role: 'Admin',
    email: 'admin@entreprise.test',
    emailVerified: true,
    ...overrides,
  }
}

// Même précaution que resetDashboardStore (voir dashboardFixtures.ts) : ne réinitialise que
// les champs de données, jamais les actions.
export function resetCurrentUserStore() {
  useCurrentUserStore.setState({
    status: 'idle',
    email: null,
    companyName: null,
    emailVerified: false,
    bannerDismissed: false,
    resendStatus: 'idle',
    resendError: null,
  })
}
