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
  createdAt: string
  // docs/specs/coquille-et-compte.md, section 6 : DELETE /api/me ne supprime l'entreprise que
  // si l'appelant en est le dernier Admin — l'écran de suppression lit cette valeur avant la
  // saisie pour annoncer le résultat qui s'applique réellement.
  isLastAdmin: boolean
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

// docs/specs/coquille-et-compte.md, section 5 : reconfirmation de l'ancien mot de passe,
// invalide les autres sessions (jamais la session courante) en cas de succès.
export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  const response = await apiFetch('/api/me/password', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ currentPassword, newPassword }),
  })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de modifier le mot de passe.'), response.status)
  }
}

// docs/specs/auth-securite-rgpd.md, section 6 : droit à la portabilité. Le serveur renvoie du
// JSON, pas un fichier — le téléchargement est construit ici, même technique que
// reportApi.downloadReport (lien temporaire vers un blob, jamais un onglet non authentifié).
export async function exportData(password: string): Promise<void> {
  const response = await apiFetch('/api/me/export', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ password }),
  })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, "Impossible d'exporter vos données."), response.status)
  }

  const data = await response.json()
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  try {
    const link = document.createElement('a')
    link.href = url
    link.download = 'mes-donnees-maat.json'
    document.body.appendChild(link)
    link.click()
    link.remove()
  } finally {
    URL.revokeObjectURL(url)
  }
}

// docs/specs/auth-securite-rgpd.md, section 6 : droit à l'effacement. `DELETE` ne fait pas
// partie des méthodes que httpClient.ts pose automatiquement en JSON (aucun autre appel de ce
// client n'envoyait de corps sur DELETE avant celui-ci) : Content-Type posé explicitement ici.
export async function deleteAccount(password: string): Promise<void> {
  const response = await apiFetch('/api/me', {
    method: 'DELETE',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ password }),
  })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de supprimer le compte.'), response.status)
  }
}
