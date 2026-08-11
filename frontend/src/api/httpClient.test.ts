import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

// docs/specs/auth-securite-rgpd.md, sections 2-3 : l'access token vit en mémoire, le
// refresh token est un cookie HttpOnly que ce code ne lit jamais (credentials: 'include'
// suffit). apiFetch() est l'unique point d'entrée pour les appels authentifiés : sur 401,
// il tente un refresh puis rejoue la requête d'origine une seule fois, et déduplique les
// refresh concurrents.

const authApi = vi.hoisted(() => ({ refresh: vi.fn() }))
vi.mock('./authApi', () => authApi)

import { apiFetch } from './httpClient'
import { getAccessToken, setAccessToken } from './tokenStore'

describe('apiFetch', () => {
  beforeEach(() => {
    setAccessToken(null)
    authApi.refresh.mockReset()
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('attache le token en mémoire en en-tête Authorization quand il est présent', async () => {
    setAccessToken('token-abc')
    vi.mocked(fetch).mockResolvedValue(new Response('{}', { status: 200 }))

    await apiFetch('/api/diagnostics/current')

    const [, init] = vi.mocked(fetch).mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(headers.get('Authorization')).toBe('Bearer token-abc')
  })

  it("envoie toujours credentials: 'include', même sans token", async () => {
    vi.mocked(fetch).mockResolvedValue(new Response('{}', { status: 200 }))

    await apiFetch('/api/diagnostics/current')

    const [, init] = vi.mocked(fetch).mock.calls[0]
    expect(init?.credentials).toBe('include')
  })

  it('ne pose pas d’en-tête Authorization sans token en mémoire', async () => {
    vi.mocked(fetch).mockResolvedValue(new Response('{}', { status: 200 }))

    await apiFetch('/api/diagnostics/current')

    const [, init] = vi.mocked(fetch).mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(headers.has('Authorization')).toBe(false)
  })

  it('sur 401, tente un refresh puis rejoue la requête une seule fois avec le nouveau token', async () => {
    setAccessToken('token-expire')
    authApi.refresh.mockResolvedValue({ accessToken: 'token-frais', expiresAt: '2026-01-01T00:00:00Z' })
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response('{}', { status: 401 }))
      .mockResolvedValueOnce(new Response('{"ok":true}', { status: 200 }))

    // authApi.refresh est mocké : il ne pose pas lui-même le token, apiFetch doit le lire
    // depuis tokenStore après coup — on simule donc ce que ferait le vrai authApi.refresh.
    authApi.refresh.mockImplementation(async () => {
      setAccessToken('token-frais')
      return { accessToken: 'token-frais', expiresAt: '2026-01-01T00:00:00Z' }
    })

    const response = await apiFetch('/api/diagnostics/current')

    expect(authApi.refresh).toHaveBeenCalledTimes(1)
    expect(fetch).toHaveBeenCalledTimes(2)
    const [, secondInit] = vi.mocked(fetch).mock.calls[1]
    const headers = new Headers(secondInit?.headers)
    expect(headers.get('Authorization')).toBe('Bearer token-frais')
    expect(response.status).toBe(200)
    expect(getAccessToken()).toBe('token-frais')
  })

  it('ne rejoue qu’une seule fois : un second 401 après refresh n’en déclenche pas un autre', async () => {
    setAccessToken('token-expire')
    authApi.refresh.mockResolvedValue({ accessToken: 'token-frais', expiresAt: '2026-01-01T00:00:00Z' })
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response('{}', { status: 401 }))
      .mockResolvedValueOnce(new Response('{}', { status: 401 }))

    const response = await apiFetch('/api/diagnostics/current')

    expect(authApi.refresh).toHaveBeenCalledTimes(1)
    expect(fetch).toHaveBeenCalledTimes(2)
    expect(response.status).toBe(401)
  })

  it('échec du refresh : retourne le 401 d’origine sans rejouer', async () => {
    setAccessToken('token-expire')
    authApi.refresh.mockResolvedValue(null)
    vi.mocked(fetch).mockResolvedValueOnce(new Response('{}', { status: 401 }))

    const response = await apiFetch('/api/diagnostics/current')

    expect(fetch).toHaveBeenCalledTimes(1)
    expect(response.status).toBe(401)
  })

  // Régression : POST /api/diagnostics sans corps recevait un 415 avant même d'atteindre le
  // contrôleur, parce que Content-Type: application/json n'était posé que si un corps était
  // fourni. ASP.NET Core lie un paramètre de corps complexe d'après l'en-tête, pas d'après la
  // présence d'octets — les quatre cas ci-dessous couvrent la règle telle que voulue : POST
  // porte toujours Content-Type (avec ou sans corps), FormData ne le reçoit jamais (le
  // navigateur doit fixer sa propre frontière multipart), GET ne le reçoit jamais.
  describe('Content-Type', () => {
    it('POST sans corps reçoit Content-Type: application/json', async () => {
      vi.mocked(fetch).mockResolvedValue(new Response('{}', { status: 200 }))

      await apiFetch('/api/diagnostics', { method: 'POST' })

      const [, init] = vi.mocked(fetch).mock.calls[0]
      const headers = new Headers(init?.headers)
      expect(headers.get('Content-Type')).toBe('application/json')
    })

    it('POST avec un corps JSON reçoit Content-Type: application/json', async () => {
      vi.mocked(fetch).mockResolvedValue(new Response('{}', { status: 200 }))

      await apiFetch('/api/diagnostics/diag-1/responses/ENV-01', {
        method: 'PUT',
        body: JSON.stringify({ value: 4 }),
      })

      const [, init] = vi.mocked(fetch).mock.calls[0]
      const headers = new Headers(init?.headers)
      expect(headers.get('Content-Type')).toBe('application/json')
    })

    it('POST avec un corps FormData ne reçoit pas Content-Type : le navigateur fixe sa propre frontière multipart', async () => {
      vi.mocked(fetch).mockResolvedValue(new Response('{}', { status: 200 }))

      await apiFetch('/api/upload', { method: 'POST', body: new FormData() })

      const [, init] = vi.mocked(fetch).mock.calls[0]
      const headers = new Headers(init?.headers)
      expect(headers.has('Content-Type')).toBe(false)
    })

    it('GET ne reçoit jamais Content-Type', async () => {
      vi.mocked(fetch).mockResolvedValue(new Response('{}', { status: 200 }))

      await apiFetch('/api/diagnostics/current')

      const [, init] = vi.mocked(fetch).mock.calls[0]
      const headers = new Headers(init?.headers)
      expect(headers.has('Content-Type')).toBe(false)
    })
  })

  it('deux 401 concurrents ne déclenchent qu’un seul refresh, les deux requêtes sont rejouées', async () => {
    let resolveRefresh!: (value: { accessToken: string; expiresAt: string }) => void
    authApi.refresh.mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveRefresh = (value) => {
            setAccessToken(value.accessToken)
            resolve(value)
          }
        }),
    )

    vi.mocked(fetch).mockImplementation(async (input) => {
      const url = typeof input === 'string' ? input : input.toString()
      if (getAccessToken() === 'token-frais') {
        return new Response(JSON.stringify({ url }), { status: 200 })
      }
      return new Response('{}', { status: 401 })
    })

    setAccessToken('token-expire')

    const call1 = apiFetch('/api/a')
    const call2 = apiFetch('/api/b')

    // Laisse les deux appels atteindre leur 401 initial et appeler ensureSingleRefresh
    // avant de résoudre le refresh — c'est ce qui prouve la déduplication.
    await Promise.resolve()
    await Promise.resolve()
    resolveRefresh({ accessToken: 'token-frais', expiresAt: '2026-01-01T00:00:00Z' })

    const [response1, response2] = await Promise.all([call1, call2])

    expect(authApi.refresh).toHaveBeenCalledTimes(1)
    expect(response1.status).toBe(200)
    expect(response2.status).toBe(200)
  })
})
