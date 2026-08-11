import { API_BASE_URL } from './config'
import { refresh as refreshTokens } from './authApi'
import { getAccessToken, notifySessionExpired } from './tokenStore'

// docs/specs/auth-securite-rgpd.md, section 3 : plusieurs 401 concurrents ne doivent
// déclencher qu'un seul appel à /api/auth/refresh — les suivants attendent la même
// promesse plutôt que d'en lancer chacun un. `refreshPromise` est partagée par toutes
// les requêtes en cours, et remise à null une fois le refresh résolu (succès ou échec).
let refreshPromise: Promise<string | null> | null = null

function ensureSingleRefresh(): Promise<string | null> {
  if (!refreshPromise) {
    refreshPromise = refreshTokens()
      .then((result) => {
        if (!result) {
          notifySessionExpired()
          return null
        }
        return result.accessToken
      })
      .catch(() => {
        notifySessionExpired()
        return null
      })
      .finally(() => {
        refreshPromise = null
      })
  }
  return refreshPromise
}

// Méthodes qui portent un corps au sens HTTP — pas GET, jamais. DELETE n'y figure pas : aucun
// appel de ce client n'envoie de corps sur DELETE aujourd'hui, et l'ajouter sans cas d'usage
// réel serait une supposition non vérifiée.
const BODY_CARRYING_METHODS = new Set(['POST', 'PUT', 'PATCH'])

// Content-Type: application/json doit être posé pour ces méthodes même sans corps (ex.
// create() sur /api/diagnostics) : ASP.NET Core lie un paramètre de corps complexe d'après le
// Content-Type de la requête, pas d'après la présence d'octets — son absence produit un 415
// avant même d'atteindre le contrôleur, indépendamment de ce que la méthode a réellement à
// envoyer. Deux exceptions : l'appelant a déjà posé son propre Content-Type (ex. un futur appel
// texte brut), ou le corps est FormData/Blob, dont le navigateur doit choisir lui-même
// l'en-tête (et, pour FormData, la frontière multipart) — le lui imposer casserait l'envoi.
function shouldSetJsonContentType(method: string, body: BodyInit | null | undefined, headers: Headers): boolean {
  if (headers.has('Content-Type')) {
    return false
  }
  if (body instanceof FormData || body instanceof Blob) {
    return false
  }
  return BODY_CARRYING_METHODS.has(method)
}

function buildRequest(path: string, options: RequestInit): [string, RequestInit] {
  const headers = new Headers(options.headers)
  const token = getAccessToken()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const method = (options.method ?? 'GET').toUpperCase()
  if (shouldSetJsonContentType(method, options.body, headers)) {
    headers.set('Content-Type', 'application/json')
  }

  return [`${API_BASE_URL}${path}`, { ...options, headers, credentials: 'include' }]
}

// Point d'entrée unique pour les appels authentifiés (les endpoints /api/auth/* passent
// par authApi.ts en fetch brut : un login refusé ne doit pas déclencher de refresh).
// Sur 401 : un refresh, puis la requête d'origine est rejouée une seule fois — jamais en
// boucle, même si la requête rejouée échoue de nouveau.
export async function apiFetch(path: string, options: RequestInit = {}): Promise<Response> {
  const response = await fetch(...buildRequest(path, options))
  if (response.status !== 401) {
    return response
  }

  const newToken = await ensureSingleRefresh()
  if (!newToken) {
    return response
  }

  return fetch(...buildRequest(path, options))
}
