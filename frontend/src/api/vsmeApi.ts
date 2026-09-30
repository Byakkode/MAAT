import { apiFetch } from './httpClient'
import { ApiError } from './authApi'

// docs/specs/norme-volontaire.md, section 4 : déclarations d'un exercice, sites de l'entreprise
// et complétude du module de base de la norme volontaire (ex-VSME).

export type VsmeDisclosure = 'B1' | 'B2' | 'B3' | 'B4' | 'B5' | 'B6' | 'B7' | 'B8' | 'B9' | 'B10' | 'B11'
export type ReportingBasis = 'Individual' | 'Consolidated'
export type EmployeeCountUnit = 'Headcount' | 'FullTimeEquivalent'
export type PollutionMedium = 'Air' | 'Water' | 'Soil'
export type SiteTenure = 'Owned' | 'Leased' | 'Managed'
export type SustainabilityTopic =
  | 'ClimateChange'
  | 'Pollution'
  | 'Water'
  | 'Biodiversity'
  | 'CircularEconomy'
  | 'Workforce'
  | 'AffectedCommunities'
  | 'ConsumersAndEndUsers'
  | 'BusinessConduct'

export interface Subsidiary {
  name: string
  registeredAddress: string
}

export interface Certification {
  name: string
  issuer: string | null
  obtainedOn: string | null
  rating: string | null
}

export interface Pollutant {
  name: string
  medium: PollutionMedium
  quantity: number
  unit: string
}

export interface CountryHeadcount {
  country: string
  employees: number
}

export interface VsmeStatement {
  reportingBasis: ReportingBasis | null
  legalForm: string | null
  totalAssetsEur: number | null
  primaryCountry: string | null
  employeeCountUnit: EmployeeCountUnit | null
  omittedDisclosures: VsmeDisclosure[]
  subsidiaries: Subsidiary[]
  certifications: Certification[]
  hasPractices: boolean | null
  hasPolicies: boolean | null
  policiesPublic: boolean | null
  hasFutureInitiatives: boolean | null
  hasTargets: boolean | null
  practicesDescription: string | null
  coveredTopics: SustainabilityTopic[]
  pollutionReportingApplicable: boolean | null
  pollutionReportUrl: string | null
  pollutants: Pollutant[]
  circularEconomyApplied: boolean | null
  circularEconomyDescription: string | null
  materialFlowsDescription: string | null
  employeesByCountry: CountryHeadcount[]
  minimumWageMet: boolean | null
  corruptionConvictions: number | null
  corruptionFinesEur: number | null
}

export const EMPTY_STATEMENT: VsmeStatement = {
  reportingBasis: null,
  legalForm: null,
  totalAssetsEur: null,
  primaryCountry: null,
  employeeCountUnit: null,
  omittedDisclosures: [],
  subsidiaries: [],
  certifications: [],
  hasPractices: null,
  hasPolicies: null,
  policiesPublic: null,
  hasFutureInitiatives: null,
  hasTargets: null,
  practicesDescription: null,
  coveredTopics: [],
  pollutionReportingApplicable: null,
  pollutionReportUrl: null,
  pollutants: [],
  circularEconomyApplied: null,
  circularEconomyDescription: null,
  materialFlowsDescription: null,
  employeesByCountry: [],
  minimumWageMet: null,
  corruptionConvictions: null,
  corruptionFinesEur: null,
}

export type DisclosureState = 'Complete' | 'Incomplete' | 'Omitted'

export interface DisclosureCompleteness {
  disclosure: VsmeDisclosure
  state: DisclosureState
  missing: string[]
}

export interface VsmeCompleteness {
  isCompliant: boolean
  isMicro: boolean
  completeCount: number
  disclosures: DisclosureCompleteness[]
}

export interface CompanySiteInput {
  name: string
  address: string
  tenure: SiteTenure
  inOrNearSensitiveArea: boolean | null
  sensitiveAreaName: string | null
}

// ADR 0014 : NotChecked (jamais vérifié ou service indisponible), None (aucune zone à 500 m),
// Found (zones dans detectedSensitiveAreas).
export type SensitiveAreaDetection = 'NotChecked' | 'None' | 'Found'

// inOrNearSensitiveArea et sensitiveAreaName (hérités de CompanySiteInput) : la réponse de
// l'utilisateur. effective* : celle que retiennent la complétude et le rapport, la sienne, sinon
// la détection automatique.
export interface CompanySite extends CompanySiteInput {
  id: string
  geocoded: boolean
  latitude: number | null
  longitude: number | null
  geocodedLabel: string | null
  sensitiveAreaDetection: SensitiveAreaDetection
  detectedSensitiveAreas: string | null
  effectiveInOrNearSensitiveArea: boolean | null
  effectiveSensitiveAreaName: string | null
  sensitiveAreaFromDetection: boolean
}

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  const body = await response.json().catch(() => null)
  return (body as { message?: string } | null)?.message ?? fallback
}

async function send<T>(url: string, method: 'PUT' | 'POST', body: unknown, fallback: string): Promise<T> {
  const response = await apiFetch(url, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, fallback), response.status)
  }
  return response.json() as Promise<T>
}

// null : rien de déclaré pour cet exercice.
export async function getStatement(year: number): Promise<VsmeStatement | null> {
  const response = await apiFetch(`/api/vsme/${year}`)
  if (response.status === 204) return null
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Erreur de chargement.'), response.status)
  }
  return response.json() as Promise<VsmeStatement>
}

export function saveStatement(year: number, statement: VsmeStatement): Promise<VsmeStatement> {
  return send(`/api/vsme/${year}`, 'PUT', statement, 'Erreur lors de la sauvegarde.')
}

export async function getCompleteness(year: number): Promise<VsmeCompleteness> {
  const response = await apiFetch(`/api/vsme/${year}/completeness`)
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Erreur de chargement.'), response.status)
  }
  return response.json() as Promise<VsmeCompleteness>
}

export async function listSites(): Promise<CompanySite[]> {
  const response = await apiFetch('/api/company/sites')
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Erreur de chargement des sites.'), response.status)
  }
  return response.json() as Promise<CompanySite[]>
}

export function createSite(input: CompanySiteInput): Promise<CompanySite> {
  return send('/api/company/sites', 'POST', input, "Impossible d'enregistrer le site.")
}

export function updateSite(id: string, input: CompanySiteInput): Promise<CompanySite> {
  return send(`/api/company/sites/${id}`, 'PUT', input, "Impossible d'enregistrer le site.")
}

export async function deleteSite(id: string): Promise<void> {
  const response = await apiFetch(`/api/company/sites/${id}`, { method: 'DELETE' })
  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response, 'Impossible de supprimer le site.'), response.status)
  }
}
