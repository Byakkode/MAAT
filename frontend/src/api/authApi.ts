import { API_BASE_URL } from './config'
import { setAccessToken } from './tokenStore'
import type { CompanySizeRange } from '../types/auth'

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

export interface RegisterPayload {
  email: string
  password: string
  companyName: string
  sectorCode: string
  sizeRange: CompanySizeRange
  region: string
}

export interface AuthTokens {
  accessToken: string
  expiresAt: string
}

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  const body = await response.json().catch(() => null)
  return (body as { message?: string } | null)?.message ?? fallback
}

export async function register(payload: RegisterPayload): Promise<{ message: string }> {
  const response = await fetch(`${API_BASE_URL}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(payload),
  })

  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, "Erreur lors de l'inscription."), response.status)
  }

  return (await response.json()) as { message: string }
}

export async function login(email: string, password: string): Promise<AuthTokens> {
  const response = await fetch(`${API_BASE_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ email, password }),
  })

  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Identifiants invalides.'), response.status)
  }

  const tokens = (await response.json()) as AuthTokens
  setAccessToken(tokens.accessToken)
  return tokens
}

// Utilisée à la fois pour le rafraîchissement silencieux au démarrage (section 2) et par
// l'intercepteur de httpClient.ts sur 401 (section 3). Une absence de session valide n'est
// pas une erreur applicative : renvoyer null plutôt que lever, y compris sur échec réseau.
export async function refresh(): Promise<AuthTokens | null> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
    })

    if (!response.ok) {
      return null
    }

    const tokens = (await response.json()) as AuthTokens
    setAccessToken(tokens.accessToken)
    return tokens
  } catch {
    return null
  }
}

// Anonyme et anti-énumération, comme register ci-dessus (auth-securite-rgpd.md, section 1) :
// appelée depuis une session authentifiée (bandeau de vérification), mais l'endpoint lui-même
// ne le suppose pas — d'où fetch brut plutôt que apiFetch, qui réserve son intercepteur 401 aux
// endpoints protégés.
export async function resendVerification(email: string): Promise<{ message: string }> {
  const response = await fetch(`${API_BASE_URL}/api/auth/resend-verification`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ email }),
  })

  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, "Erreur lors du renvoi de l'e-mail de vérification."), response.status)
  }

  return (await response.json()) as { message: string }
}

export async function logout(): Promise<void> {
  await fetch(`${API_BASE_URL}/api/auth/logout`, {
    method: 'POST',
    credentials: 'include',
  }).catch(() => {})
  setAccessToken(null)
}
