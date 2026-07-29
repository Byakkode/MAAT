// docs/specs/auth-securite-rgpd.md, section 2 : l'access token vit en mémoire JS
// uniquement, jamais dans localStorage ni sessionStorage — une variable de module perd
// son contenu au rechargement de page, ce qui est le comportement attendu.
let accessToken: string | null = null

export function getAccessToken(): string | null {
  return accessToken
}

export function setAccessToken(token: string | null): void {
  accessToken = token
}

type SessionExpiredListener = () => void

const listeners = new Set<SessionExpiredListener>()

export function onSessionExpired(listener: SessionExpiredListener): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

// Appelé quand un refresh échoue : purge le token et prévient les abonnés (le store
// d'authentification, pour rediriger vers la connexion) sans dépendre de React ici.
export function notifySessionExpired(): void {
  accessToken = null
  listeners.forEach((listener) => listener())
}
