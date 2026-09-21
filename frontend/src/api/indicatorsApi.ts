import { apiFetch } from './httpClient'
import { ApiError } from './authApi'

export interface RseIndicators {
  co2EmissionsTons: number | null
  energyConsumptionKwh: number | null
  renewableEnergyPct: number | null
  waterConsumptionM3: number | null
  wasteTons: number | null
  recyclingRatePct: number | null
  employeeCountFte: number | null
  turnoverRatePct: number | null
  trainingHoursPerEmployee: number | null
  workAccidentRate: number | null
  genderEqualityIndex: number | null
  permanentContractPct: number | null
  localSuppliersPct: number | null
  rseAssessedSuppliersPct: number | null
  activeSuppliersCount: number | null
  revenueEur: number | null
  rseInvestmentEur: number | null
  exportRevenuePct: number | null
}

export const EMPTY_INDICATORS: RseIndicators = {
  co2EmissionsTons: null,
  energyConsumptionKwh: null,
  renewableEnergyPct: null,
  waterConsumptionM3: null,
  wasteTons: null,
  recyclingRatePct: null,
  employeeCountFte: null,
  turnoverRatePct: null,
  trainingHoursPerEmployee: null,
  workAccidentRate: null,
  genderEqualityIndex: null,
  permanentContractPct: null,
  localSuppliersPct: null,
  rseAssessedSuppliersPct: null,
  activeSuppliersCount: null,
  revenueEur: null,
  rseInvestmentEur: null,
  exportRevenuePct: null,
}

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  const body = await response.json().catch(() => null)
  return (body as { message?: string } | null)?.message ?? fallback
}

export async function getIndicators(year: number): Promise<RseIndicators | null> {
  const response = await apiFetch(`/api/indicators/${year}`)
  if (response.status === 204) return null
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Erreur de chargement.'), response.status)
  }
  return response.json() as Promise<RseIndicators>
}

export async function upsertIndicators(year: number, data: RseIndicators): Promise<RseIndicators> {
  const response = await apiFetch(`/api/indicators/${year}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Erreur lors de la sauvegarde.'), response.status)
  }
  return response.json() as Promise<RseIndicators>
}

export async function getYearsWithData(): Promise<number[]> {
  const response = await apiFetch('/api/indicators/years')
  if (!response.ok) return []
  return response.json() as Promise<number[]>
}
