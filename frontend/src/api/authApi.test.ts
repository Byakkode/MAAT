import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, login, logout, refresh, register } from './authApi'
import { getAccessToken, setAccessToken } from './tokenStore'

describe('authApi', () => {
  beforeEach(() => {
    setAccessToken(null)
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  describe('register', () => {
    it('poste les champs d’inscription et retourne le message générique', async () => {
      vi.mocked(fetch).mockResolvedValue(
        new Response(JSON.stringify({ message: 'Vérifiez votre boîte mail.' }), { status: 201 }),
      )

      const result = await register({
        email: 'admin@entreprise.test',
        password: 'MotDePasseValide2026!',
        companyName: 'Entreprise Test',
        sectorCode: '6201Z',
        sizeRange: 'Micro',
        region: 'Île-de-France',
      })

      expect(result.message).toBe('Vérifiez votre boîte mail.')
      const [url, init] = vi.mocked(fetch).mock.calls[0]
      expect(url).toBe('http://localhost:5130/api/auth/register')
      expect(init?.credentials).toBe('include')
      expect(JSON.parse(init?.body as string)).toEqual({
        email: 'admin@entreprise.test',
        password: 'MotDePasseValide2026!',
        companyName: 'Entreprise Test',
        sectorCode: '6201Z',
        sizeRange: 'Micro',
        region: 'Île-de-France',
      })
    })

    it('propage le message d’erreur du serveur en cas de 400', async () => {
      vi.mocked(fetch).mockResolvedValue(
        new Response(JSON.stringify({ message: 'Mot de passe trop court.' }), { status: 400 }),
      )

      await expect(
        register({
          email: 'a@test.test',
          password: 'court',
          companyName: 'X',
          sectorCode: '6201Z',
          sizeRange: 'Micro',
          region: 'Île-de-France',
        }),
      ).rejects.toThrow('Mot de passe trop court.')
    })
  })

  describe('login', () => {
    it('poste les identifiants et place le token reçu en mémoire', async () => {
      vi.mocked(fetch).mockResolvedValue(
        new Response(JSON.stringify({ accessToken: 'jwt-1', expiresAt: '2026-01-01T00:15:00Z' }), { status: 200 }),
      )

      const tokens = await login('admin@entreprise.test', 'MotDePasseValide2026!')

      expect(tokens.accessToken).toBe('jwt-1')
      expect(getAccessToken()).toBe('jwt-1')
      const [url, init] = vi.mocked(fetch).mock.calls[0]
      expect(url).toBe('http://localhost:5130/api/auth/login')
      expect(init?.credentials).toBe('include')
      expect(JSON.parse(init?.body as string)).toEqual({
        email: 'admin@entreprise.test',
        password: 'MotDePasseValide2026!',
      })
    })

    it('lève une ApiError avec le message générique sur 401, ne pose pas de token', async () => {
      vi.mocked(fetch).mockResolvedValue(
        new Response(JSON.stringify({ message: 'Identifiants invalides.' }), { status: 401 }),
      )

      await expect(login('a@test.test', 'mauvais')).rejects.toBeInstanceOf(ApiError)
      expect(getAccessToken()).toBeNull()
    })
  })

  describe('refresh', () => {
    it('pose le nouveau token en mémoire sur succès', async () => {
      vi.mocked(fetch).mockResolvedValue(
        new Response(JSON.stringify({ accessToken: 'jwt-2', expiresAt: '2026-01-01T00:15:00Z' }), { status: 200 }),
      )

      const tokens = await refresh()

      expect(tokens?.accessToken).toBe('jwt-2')
      expect(getAccessToken()).toBe('jwt-2')
      const [, init] = vi.mocked(fetch).mock.calls[0]
      expect(init?.credentials).toBe('include')
    })

    it('retourne null sans lever quand le cookie est absent ou invalide (401)', async () => {
      vi.mocked(fetch).mockResolvedValue(new Response('{}', { status: 401 }))

      await expect(refresh()).resolves.toBeNull()
    })

    it('retourne null sans lever en cas d’échec réseau', async () => {
      vi.mocked(fetch).mockRejectedValue(new TypeError('Failed to fetch'))

      await expect(refresh()).resolves.toBeNull()
    })
  })

  describe('logout', () => {
    it('appelle /api/auth/logout et purge le token en mémoire', async () => {
      setAccessToken('jwt-3')
      vi.mocked(fetch).mockResolvedValue(new Response(null, { status: 204 }))

      await logout()

      expect(getAccessToken()).toBeNull()
      const [url, init] = vi.mocked(fetch).mock.calls[0]
      expect(url).toBe('http://localhost:5130/api/auth/logout')
      expect(init?.credentials).toBe('include')
    })

    it('purge quand même le token si l’appel réseau échoue', async () => {
      setAccessToken('jwt-4')
      vi.mocked(fetch).mockRejectedValue(new TypeError('Failed to fetch'))

      await logout()

      expect(getAccessToken()).toBeNull()
    })
  })
})
