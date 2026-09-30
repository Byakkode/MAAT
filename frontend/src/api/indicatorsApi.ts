import { apiFetch } from './httpClient'
import { ApiError } from './authApi'

export interface RseIndicators {
  co2EmissionsTons: number | null
  energyConsumptionKwh: number | null
  renewableEnergyPct: number | null
  waterConsumptionM3: number | null
  wasteTons: number | null
  recyclingRatePct: number | null
  // docs/specs/norme-volontaire.md, section 2 : données du module de base.
  electricityRenewableMwh: number | null
  electricityNonRenewableMwh: number | null
  fuelsRenewableMwh: number | null
  fuelsNonRenewableMwh: number | null
  scope1Tco2e: number | null
  scope2LocationTco2e: number | null
  waterWithdrawalM3: number | null
  waterConsumptionStressM3: number | null
  hazardousWasteTons: number | null
  nonHazardousWasteTons: number | null
  permanentEmployees: number | null
  temporaryEmployees: number | null
  femaleEmployees: number | null
  maleEmployees: number | null
  otherGenderEmployees: number | null
  recordableAccidents: number | null
  hoursWorked: number | null
  workFatalities: number | null
  genderPayGapPct: number | null
  collectiveBargainingPct: number | null
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
  electricityRenewableMwh: null,
  electricityNonRenewableMwh: null,
  fuelsRenewableMwh: null,
  fuelsNonRenewableMwh: null,
  scope1Tco2e: null,
  scope2LocationTco2e: null,
  waterWithdrawalM3: null,
  waterConsumptionStressM3: null,
  hazardousWasteTons: null,
  nonHazardousWasteTons: null,
  permanentEmployees: null,
  temporaryEmployees: null,
  femaleEmployees: null,
  maleEmployees: null,
  otherGenderEmployees: null,
  recordableAccidents: null,
  hoursWorked: null,
  workFatalities: null,
  genderPayGapPct: null,
  collectiveBargainingPct: null,
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
