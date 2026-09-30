import type { SiteTenure, SustainabilityTopic, VsmeDisclosure } from '../api/vsmeApi'

// Mêmes libellés que backend/MAAT.Domain/Enums/VsmeDisclosureLabels.cs : l'écran de saisie et
// le rapport PDF doivent nommer une information de la même façon (norme-volontaire.md).
export const VSME_DISCLOSURE_LABELS: Record<VsmeDisclosure, string> = {
  B1: "Base d'établissement du rapport",
  B2: 'Pratiques, politiques et initiatives futures pour une économie plus durable',
  B3: 'Énergie et émissions de gaz à effet de serre',
  B4: "Pollution de l'air, de l'eau et du sol",
  B5: 'Biodiversité',
  B6: 'Eau',
  B7: 'Ressources, économie circulaire et gestion des déchets',
  B8: 'Effectifs : caractéristiques générales',
  B9: 'Effectifs : santé et sécurité',
  B10: 'Effectifs : rémunération, négociation collective et formation',
  B11: 'Condamnations et amendes pour corruption',
}

export const VSME_DISCLOSURES = Object.keys(VSME_DISCLOSURE_LABELS) as VsmeDisclosure[]

// backend/MAAT.Domain/Enums/SustainabilityTopicLabels.cs (annexe B de la norme).
export const SUSTAINABILITY_TOPIC_LABELS: Record<SustainabilityTopic, string> = {
  ClimateChange: 'Changement climatique',
  Pollution: 'Pollution',
  Water: 'Eau',
  Biodiversity: 'Biodiversité et écosystèmes',
  CircularEconomy: 'Économie circulaire et utilisation des ressources',
  Workforce: 'Effectifs et travailleurs de la chaîne de valeur',
  AffectedCommunities: 'Communautés affectées',
  ConsumersAndEndUsers: 'Consommateurs et utilisateurs finaux',
  BusinessConduct: 'Conduite des affaires',
}

export const SITE_TENURE_LABELS: Record<SiteTenure, string> = {
  Owned: 'Détenu',
  Leased: 'Loué',
  Managed: 'Géré',
}

export const LEGAL_FORM_SUGGESTIONS = ['SAS', 'SASU', 'SARL', 'EURL', 'SA', 'SNC', 'SCOP', 'SCIC', 'Entreprise individuelle', 'Association']

// docs/specs/norme-volontaire.md, section 5 : les onglets suivent les quatre groupes de la
// norme (informations générales, environnement, social, gouvernance), puis les compléments.
export type VsmeTab = 'general' | 'environment' | 'social' | 'governance' | 'complements'

export const VSME_GROUPS: { id: Exclude<VsmeTab, 'complements'>; label: string; codes: VsmeDisclosure[] }[] = [
  { id: 'general', label: 'Général', codes: ['B1', 'B2'] },
  { id: 'environment', label: 'Environnement', codes: ['B3', 'B4', 'B5', 'B6', 'B7'] },
  { id: 'social', label: 'Social', codes: ['B8', 'B9', 'B10'] },
  { id: 'governance', label: 'Gouvernance', codes: ['B11'] },
]
