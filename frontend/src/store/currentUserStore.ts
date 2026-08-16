import { create } from 'zustand'
import * as accountApi from '../api/accountApi'
import * as authApi from '../api/authApi'

export type CurrentUserStatus = 'idle' | 'loading' | 'loaded' | 'error'
export type ResendStatus = 'idle' | 'sending' | 'sent' | 'error'

interface CurrentUserState {
  status: CurrentUserStatus
  email: string | null
  companyName: string | null
  createdAt: string | null
  emailVerified: boolean
  // docs/specs/coquille-et-compte.md, section 4 : "Le bandeau se ferme pour la session, jamais
  // définitivement." — un simple drapeau en mémoire suffit : il retombe à false à chaque
  // rechargement complet de page, jamais persisté (localStorage réapparaîtrait "pour la
  // session" au sens littéral, mais survivrait à un rechargement, ce que la spec exclut).
  bannerDismissed: boolean
  resendStatus: ResendStatus
  resendError: string | null

  load: () => Promise<void>
  dismissBanner: () => void
  resendVerificationEmail: () => Promise<void>
}

export const useCurrentUserStore = create<CurrentUserState>((set, get) => ({
  status: 'idle',
  email: null,
  companyName: null,
  createdAt: null,
  emailVerified: false,
  bannerDismissed: false,
  resendStatus: 'idle',
  resendError: null,

  async load() {
    set({ status: 'loading' })
    try {
      const user = await accountApi.getCurrentUser()
      set({
        status: 'loaded',
        email: user.email,
        companyName: user.companyName,
        createdAt: user.createdAt,
        emailVerified: user.emailVerified,
      })
    } catch {
      set({ status: 'error' })
    }
  },

  dismissBanner() {
    set({ bannerDismissed: true })
  },

  async resendVerificationEmail() {
    const { email } = get()
    if (!email) {
      return
    }

    set({ resendStatus: 'sending', resendError: null })
    try {
      await authApi.resendVerification(email)
      set({ resendStatus: 'sent' })
    } catch (err) {
      const message = err instanceof Error ? err.message : "Erreur lors du renvoi de l'e-mail de vérification."
      set({ resendStatus: 'error', resendError: message })
    }
  },
}))
