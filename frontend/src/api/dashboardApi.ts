import { apiFetch } from './httpClient'
import { ApiError } from './authApi'
import type { DashboardView } from '../types/dashboard'

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  const body = await response.json().catch(() => null)
  return (body as { message?: string } | null)?.message ?? fallback
}

// docs/specs/dashboard.md, section 1 : un seul appel retourne tout l'écran, toujours 200 —
// aucun cas 404/vide à distinguer ici, contrairement à diagnosticsApi.getCurrent.
export async function getDashboard(): Promise<DashboardView> {
  const response = await apiFetch('/api/dashboard')
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de récupérer le tableau de bord.'), response.status)
  }
  return (await response.json()) as DashboardView
}
