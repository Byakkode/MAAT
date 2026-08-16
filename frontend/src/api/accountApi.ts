import { apiFetch } from './httpClient'
import { ApiError } from './authApi'

// Miroir de MAAT.Api.Controllers.AuthController.Me(). Distinct de authApi.ts, qui réserve le
// fetch brut aux seuls endpoints d'authentification eux-mêmes (login/register/refresh/logout —
// voir httpClient.ts) : /api/auth/me est un endpoint protégé ordinaire, il passe donc par
// apiFetch comme dashboardApi/recommendationsApi, avec le rafraîchissement automatique sur 401.
// emailVerified n'est délibérément pas un claim JWT (auth-securite-rgpd.md, section 2), d'où ce
// second aller-retour — nécessaire au bouton de téléchargement du rapport (rapport-pdf.md,
// section 6), qui doit savoir si l'adresse est vérifiée avant même la première tentative.
export interface CurrentUser {
  userId: string
  companyId: string
  companyName: string
  role: string
  email: string
  emailVerified: boolean
}

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  const body = await response.json().catch(() => null)
  return (body as { message?: string } | null)?.message ?? fallback
}

export async function getCurrentUser(): Promise<CurrentUser> {
  const response = await apiFetch('/api/auth/me')
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de récupérer le profil.'), response.status)
  }
  return (await response.json()) as CurrentUser
}
