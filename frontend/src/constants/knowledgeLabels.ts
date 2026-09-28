import type { KnowledgeCategory, KnowledgeLevel } from '../api/documentationApi'

// docs/specs/documentation.md, section 1 : rubriques de la base documentaire, dans l'ordre
// du serveur (KnowledgeCategory, du plus accessible au plus spécialisé).
export const KNOWLEDGE_CATEGORY_LABELS: Record<KnowledgeCategory, string> = {
  GettingStarted: 'Premiers pas',
  Regulation: 'Réglementation',
  Environment: 'Environnement',
  Social: 'Social',
  BusinessEthics: 'Éthique des affaires',
  Procurement: 'Achats responsables',
  Governance: 'Gouvernance et stratégie',
  Standards: 'Référentiels et labels',
  Funding: 'Financer sa démarche',
}

// Deux publics : le dirigeant qui découvre la RSE, le professionnel qui cherche la précision.
export const KNOWLEDGE_LEVEL_LABELS: Record<KnowledgeLevel, string> = {
  Essentials: "L'essentiel",
  Expert: 'Expert',
}

const dateFormat = new Intl.DateTimeFormat('fr-FR', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' })

// « 2026-09-28 » → « 28 septembre 2026 » (date seule, lue en UTC pour ne jamais glisser d'un jour).
export function formatKnowledgeDate(isoDate: string): string {
  return dateFormat.format(new Date(`${isoDate}T00:00:00Z`))
}
