import type { RseDomain } from '../types/questionnaire'
import { DOMAIN_COLORS } from '../constants/domainColors'

// La landing ne montre que des données réelles du produit : libellés et ancrages normatifs
// de docs/specs/modele-donnees.md, pondérations de backend/MAAT.Infrastructure/Seed/
// sector-weights.csv. Aucune métrique marketing inventée (clients, taux de satisfaction…) :
// devant un jury comme devant un prospect, un chiffre qu'on ne peut pas sourcer affaiblit
// tous les autres. À mettre à jour si sector-weights.csv change.

export interface DomainInfo {
  key: RseDomain
  label: string
  anchor: string
  color: string
}

export const DOMAINS: readonly DomainInfo[] = [
  { key: 'Environmental', label: 'Environnement', anchor: 'VSME B3–B7 · ESRS E1–E5', color: DOMAIN_COLORS.Environmental },
  { key: 'Social', label: 'Social & droits humains', anchor: 'VSME B8–B10 · ESRS S1–S4', color: DOMAIN_COLORS.Social },
  { key: 'Ethics', label: 'Éthique des affaires', anchor: 'VSME B11 · ESRS G1', color: DOMAIN_COLORS.Ethics },
  { key: 'Procurement', label: 'Achats responsables', anchor: 'ISO 26000 §6.6.6 · EcoVadis', color: DOMAIN_COLORS.Procurement },
  { key: 'Governance', label: 'Gouvernance & pilotage', anchor: 'VSME B1–B2, C1, C9', color: DOMAIN_COLORS.Governance },
]

export interface SectorInfo {
  code: string
  label: string
  weights: Record<RseDomain, number>
}

// Sélection de sections NAF dont les profils de pondération diffèrent nettement : l'objectif
// est de montrer que le même score brut ne pèse pas pareil d'un secteur à l'autre.
export const SECTORS: readonly SectorInfo[] = [
  { code: 'C', label: 'Industrie manufacturière', weights: { Environmental: 0.3, Social: 0.2, Ethics: 0.15, Procurement: 0.25, Governance: 0.1 } },
  { code: 'F', label: 'Construction', weights: { Environmental: 0.25, Social: 0.35, Ethics: 0.1, Procurement: 0.25, Governance: 0.05 } },
  { code: 'G', label: 'Commerce', weights: { Environmental: 0.15, Social: 0.2, Ethics: 0.2, Procurement: 0.35, Governance: 0.1 } },
  { code: 'I', label: 'Hébergement et restauration', weights: { Environmental: 0.2, Social: 0.35, Ethics: 0.15, Procurement: 0.2, Governance: 0.1 } },
  { code: 'J', label: 'Information et communication', weights: { Environmental: 0.1, Social: 0.25, Ethics: 0.3, Procurement: 0.1, Governance: 0.25 } },
  { code: 'Q', label: 'Santé humaine et action sociale', weights: { Environmental: 0.1, Social: 0.4, Ethics: 0.25, Procurement: 0.15, Governance: 0.1 } },
]

// Scores de domaine de l'entreprise fictive affichée dans les maquettes. Le score global
// n'est pas saisi à la main : il est recalculé avec la pondération du secteur C, comme le
// ferait ScoringService, pour que la maquette reste cohérente si l'un des chiffres change.
export const DEMO_DOMAIN_SCORES: Record<RseDomain, number> = {
  Environmental: 61,
  Social: 74,
  Ethics: 58,
  Procurement: 49,
  Governance: 72,
}

export function weightedScore(scores: Record<RseDomain, number>, weights: Record<RseDomain, number>): number {
  const total = DOMAINS.reduce((sum, d) => sum + scores[d.key] * weights[d.key], 0)
  return Math.round(total)
}
