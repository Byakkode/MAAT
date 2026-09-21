import { create } from 'zustand'
import * as authApi from '../api/authApi'
import type { RegisterPayload } from '../api/authApi'
import { onSessionExpired } from '../api/tokenStore'
import { decodeJwt } from '../lib/jwt'

export type AuthStatus = 'restoring' | 'authenticated' | 'unauthenticated'

export interface AuthUser {
  userId: string
  companyId: string
  role: string
}

interface AuthState {
  status: AuthStatus
  user: AuthUser | null
  error: string | null
  restoreSession: () => Promise<void>
  login: (email: string, password: string) => Promise<void>
  register: (payload: RegisterPayload) => Promise<{ message: string }>
  logout: () => Promise<void>
}

function userFromToken(accessToken: string): AuthUser {
  const claims = decodeJwt(accessToken)
  return { userId: claims.sub, companyId: claims.company_id, role: claims.role }
}

export const useAuthStore = create<AuthState>((set) => {
  // docs/specs/auth-securite-rgpd.md, section 3 : un refresh qui échoue (dans
  // l'intercepteur de httpClient.ts comme au démarrage) purge la session ici, hors de
  // tout composant React, pour que la redirection vers /login s'applique partout.
  onSessionExpired(() => set({ status: 'unauthenticated', user: null }))

  return {
    status: 'restoring',
    user: null,
    error: null,

    async restoreSession() {
      const tokens = await authApi.refresh()
      if (!tokens) {
        // Ne pas écraser 'authenticated' : en React 18 StrictMode, l'effet App.tsx
        // se déclenche deux fois au montage — si le second appel résout APRÈS que
        // login() ait posé 'authenticated', il renvoyait l'utilisateur sur /login.
        set((state) =>
          state.status === 'restoring' ? { status: 'unauthenticated', user: null } : state,
        )
        return
      }
      set({ status: 'authenticated', user: userFromToken(tokens.accessToken), error: null })
    },

    async login(email, password) {
      set({ error: null })
      try {
        const tokens = await authApi.login(email, password)
        set({ status: 'authenticated', user: userFromToken(tokens.accessToken), error: null })
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Erreur de connexion.'
        set({ status: 'unauthenticated', user: null, error: message })
        throw err
      }
    },

    async register(payload) {
      set({ error: null })
      try {
        return await authApi.register(payload)
      } catch (err) {
        const message = err instanceof Error ? err.message : "Erreur lors de l'inscription."
        set({ error: message })
        throw err
      }
    },

    async logout() {
      await authApi.logout()
      set({ status: 'unauthenticated', user: null })
    },
  }
})
