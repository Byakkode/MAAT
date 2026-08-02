// Miroir de MAAT.Domain.Enums.RseDomain (backend/MAAT.Domain/Enums/RseDomain.cs), sérialisé
// en chaîne par System.Text.Json : les valeurs doivent correspondre exactement aux noms des
// membres C#. Ordre fixe imposé par docs/specs/questionnaire.md, section 2.
export type RseDomain = 'Environmental' | 'Social' | 'Ethics' | 'Procurement' | 'Governance'

export const DOMAIN_ORDER: readonly RseDomain[] = ['Environmental', 'Social', 'Ethics', 'Procurement', 'Governance']

// docs/specs/modele-donnees.md, section sur RseDomain.
export const DOMAIN_LABELS: Record<RseDomain, string> = {
  Environmental: 'Environnement',
  Social: 'Social & droits humains',
  Ethics: 'Éthique des affaires',
  Procurement: 'Achats responsables',
  Governance: 'Gouvernance & pilotage',
}

// Miroir de MAAT.Domain.Enums.DiagnosticStatus.
export type DiagnosticStatus = 'InProgress' | 'Completed' | 'Archived'

// Miroir de la projection retournée par GET /api/diagnostics/{id}/questions
// (MAAT.Application.DTOs.QuestionAnswerView) : value est nul tant que la question n'a pas de
// réponse enregistrée sur ce diagnostic.
export interface QuestionAnswer {
  code: string
  text: string
  helpText: string | null
  domain: RseDomain
  displayOrder: number
  value: number | null
}

// Miroir de la réponse de GET /api/diagnostics/current.
export interface DiagnosticSummary {
  id: string
  companyId: string
  status: DiagnosticStatus
  createdAt: string
  answeredCount: number
  totalActiveQuestions: number
}

// Miroir de la réponse de GET /api/diagnostics/{id} : seule source fiable du statut réel
// d'un diagnostic donné (docs/specs/questionnaire.md, section 1). Ne jamais déduire ce
// statut d'une comparaison indirecte (ex. avec l'identifiant retourné par /current).
export interface DiagnosticDetail {
  id: string
  companyId: string
  status: DiagnosticStatus
  globalScore: number | null
  createdAt: string
  completedAt: string | null
}
