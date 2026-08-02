import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const authApi = vi.hoisted(() => ({
  login: vi.fn(),
  register: vi.fn(),
  refresh: vi.fn(),
  logout: vi.fn(),
  ApiError: class ApiError extends Error {
    status: number
    constructor(message: string, status: number) {
      super(message)
      this.status = status
    }
  },
}))
vi.mock('../api/authApi', () => authApi)

import { useAuthStore } from './authStore'
import { notifySessionExpired, setAccessToken } from '../api/tokenStore'

function makeToken(claims: Record<string, unknown>): string {
  const header = btoa('{"alg":"HS256"}')
  const body = btoa(JSON.stringify(claims))
  return `${header}.${body}.sig`
}

describe('useAuthStore', () => {
  beforeEach(() => {
    setAccessToken(null)
    authApi.login.mockReset()
    authApi.register.mockReset()
    authApi.refresh.mockReset()
    authApi.logout.mockReset()
    useAuthStore.setState({ status: 'restoring', user: null, error: null })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('restoreSession', () => {
    it('passe à authenticated avec les claims du token quand le refresh silencieux réussit', async () => {
      authApi.refresh.mockResolvedValue({
        accessToken: makeToken({ sub: 'user-1', company_id: 'company-1', role: 'Admin' }),
        expiresAt: '2026-01-01T00:15:00Z',
      })

      await useAuthStore.getState().restoreSession()

      const state = useAuthStore.getState()
      expect(state.status).toBe('authenticated')
      expect(state.user).toEqual({ userId: 'user-1', companyId: 'company-1', role: 'Admin' })
    })

    it('passe à unauthenticated quand aucune session ne peut être restaurée', async () => {
      authApi.refresh.mockResolvedValue(null)

      await useAuthStore.getState().restoreSession()

      const state = useAuthStore.getState()
      expect(state.status).toBe('unauthenticated')
      expect(state.user).toBeNull()
    })
  })

  describe('login', () => {
    it('passe à authenticated sur succès', async () => {
      authApi.login.mockResolvedValue({
        accessToken: makeToken({ sub: 'user-2', company_id: 'company-2', role: 'User' }),
        expiresAt: '2026-01-01T00:15:00Z',
      })

      await useAuthStore.getState().login('user@entreprise.test', 'MotDePasseValide2026!')

      const state = useAuthStore.getState()
      expect(state.status).toBe('authenticated')
      expect(state.user).toEqual({ userId: 'user-2', companyId: 'company-2', role: 'User' })
      expect(authApi.login).toHaveBeenCalledWith('user@entreprise.test', 'MotDePasseValide2026!')
    })

    it('reste unauthenticated et expose le message d’erreur sur échec', async () => {
      authApi.login.mockRejectedValue(new authApi.ApiError('Identifiants invalides.', 401))

      await expect(
        useAuthStore.getState().login('user@entreprise.test', 'mauvais'),
      ).rejects.toThrow('Identifiants invalides.')

      const state = useAuthStore.getState()
      expect(state.status).toBe('unauthenticated')
      expect(state.error).toBe('Identifiants invalides.')
    })
  })

  describe('logout', () => {
    it('appelle authApi.logout et repasse à unauthenticated', async () => {
      useAuthStore.setState({ status: 'authenticated', user: { userId: 'u', companyId: 'c', role: 'Admin' } })
      authApi.logout.mockResolvedValue(undefined)

      await useAuthStore.getState().logout()

      expect(authApi.logout).toHaveBeenCalled()
      const state = useAuthStore.getState()
      expect(state.status).toBe('unauthenticated')
      expect(state.user).toBeNull()
    })
  })

  describe('réaction à l’expiration de session', () => {
    it('passe à unauthenticated quand tokenStore signale une session expirée', () => {
      useAuthStore.setState({ status: 'authenticated', user: { userId: 'u', companyId: 'c', role: 'Admin' } })

      notifySessionExpired()

      const state = useAuthStore.getState()
      expect(state.status).toBe('unauthenticated')
      expect(state.user).toBeNull()
    })
  })
})
