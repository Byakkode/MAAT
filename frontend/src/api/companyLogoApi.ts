import { apiFetch } from './httpClient'
import { ApiError } from './authApi'

// docs/specs/rapport-pdf.md, section 7 : logo de l'entreprise. Jamais d'URL publique : l'image
// est lue par une requête authentifiée, en blob, comme le rapport lui-même (reportApi.ts).

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  const body = await response.json().catch(() => null)
  return (body as { message?: string } | null)?.message ?? fallback
}

// Limite du serveur (CompanyLogoService.MaxUploadBytes), vérifiée aussi avant l'envoi pour
// répondre tout de suite plutôt qu'après le transfert d'un fichier refusé d'avance.
export const MAX_LOGO_BYTES = 2 * 1024 * 1024
export const ACCEPTED_LOGO_TYPES = 'image/png,image/jpeg,image/webp'

// null : aucun logo enregistré.
export async function fetchCompanyLogo(): Promise<Blob | null> {
  const response = await apiFetch('/api/company/logo')
  if (response.status === 404) {
    return null
  }
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de charger le logo.'), response.status)
  }
  return response.blob()
}

export async function uploadCompanyLogo(file: File): Promise<void> {
  const form = new FormData()
  form.append('file', file)
  const response = await apiFetch('/api/company/logo', { method: 'PUT', body: form })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, "Impossible d'enregistrer le logo."), response.status)
  }
}

export async function deleteCompanyLogo(): Promise<void> {
  const response = await apiFetch('/api/company/logo', { method: 'DELETE' })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de retirer le logo.'), response.status)
  }
}
