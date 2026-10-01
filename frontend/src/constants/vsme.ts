import type { RseIndicators } from '../api/indicatorsApi'
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

// « Où trouver ? » (norme-volontaire.md, section 5) : le document où chercher chaque donnée
// chiffrée, pour une PME française. Seulement là où la réponse n'est pas évidente.
export const VSME_DATA_SOURCES: Partial<Record<keyof RseIndicators | 'totalAssetsEur', string>> = {
  totalAssetsEur:
    'Dans le bilan de votre dernier exercice (liasse fiscale, formulaire 2050, ou 2033-A au régime simplifié) : le total de l’actif net. Votre expert-comptable peut vous le transmettre.',
  revenueEur:
    'Dans le compte de résultat de votre dernier exercice (formulaire 2052, ou 2033-B au régime simplifié) : le chiffre d’affaires net.',
  employeeCountFte:
    'Votre logiciel de paie ou votre gestionnaire de paie : l’effectif moyen annuel en équivalent temps plein.',
  energyConsumptionKwh:
    'Additionnez les consommations de l’année figurant sur vos factures d’électricité et de gaz (en kWh), ou demandez le relevé annuel dans votre espace client. Fioul et carburants : les volumes livrés, à convertir en kWh (un litre de gazole vaut environ 10 kWh).',
  electricityRenewableMwh:
    'Votre contrat d’électricité : une offre verte adossée à des garanties d’origine compte comme renouvelable, tout comme l’électricité de vos propres panneaux solaires. 1 MWh = 1 000 kWh.',
  electricityNonRenewableMwh:
    'L’électricité achetée hors offre verte : la consommation de vos factures, convertie en MWh (1 MWh = 1 000 kWh).',
  fuelsRenewableMwh:
    'Bois, granulés, biogaz ou biocarburants : factures ou bons de livraison, convertis en MWh (le fournisseur indique souvent l’équivalent énergétique).',
  fuelsNonRenewableMwh:
    'Gaz naturel, fioul, carburants des véhicules : factures de gaz, bons de livraison de fioul, relevés de cartes carburant, convertis en MWh.',
  scope1Tco2e:
    'Ce chiffre vient d’un bilan carbone (bilan GES). Si vous en avez réalisé un, reportez son total Scope 1. Sinon, laissez vide : B3 restera à compléter dans votre rapport. Le Diag Décarbon’Action de Bpifrance et de l’ADEME subventionne un premier bilan carbone accompagné pour les PME.',
  scope2LocationTco2e:
    'Même source que le Scope 1 : le total Scope 2 de votre bilan carbone, calculé selon la méthode fondée sur la localisation (facteur moyen du réseau électrique).',
  waterWithdrawalM3:
    'Additionnez les volumes de l’année sur vos factures d’eau (relevés du compteur, en m³). Si vous avez un forage ou un prélèvement en rivière, ajoutez les volumes déclarés à l’agence de l’eau.',
  waterConsumptionM3:
    'Le volume prélevé moins le volume rejeté par vos procédés (eau évaporée ou incorporée aux produits) : compteurs dédiés de vos installations, ou estimation de votre responsable de production.',
  hazardousWasteTons:
    'Vos bordereaux de suivi des déchets dangereux, sur la plateforme publique Trackdéchets : le poids figure sur chaque bordereau, et Trackdéchets en fait le total annuel.',
  nonHazardousWasteTons:
    'Votre registre des déchets, obligatoire pour les producteurs, ou les relevés annuels de votre prestataire de collecte. Les déchets collectés par la commune ne sont pas pesés : une estimation suffit.',
  recyclingRatePct:
    'L’attestation annuelle que votre prestataire de collecte doit vous remettre pour le tri des papiers, plastiques, métaux, verre et bois : elle indique les quantités recyclées ou valorisées.',
  permanentEmployees:
    'Registre unique du personnel ou logiciel de paie, à la date de clôture de l’exercice : comptez les CDI.',
  temporaryEmployees:
    'Même source : les CDD, y compris les contrats d’apprentissage et de professionnalisation à durée limitée. Les intérimaires ne sont pas vos salariés.',
  femaleEmployees: 'Registre unique du personnel ou logiciel de paie. Si vous publiez l’index Egapro, vous avez déjà ces effectifs.',
  maleEmployees: 'Registre unique du personnel ou logiciel de paie. Si vous publiez l’index Egapro, vous avez déjà ces effectifs.',
  recordableAccidents:
    'Vos déclarations d’accident du travail à l’Assurance maladie et votre registre des accidents bénins : comptez ceux qui ont entraîné un décès ou plus de trois jours d’absence. Le compte AT/MP sur net-entreprises.fr les récapitule.',
  workFatalities: 'Mêmes sources que les accidents du travail.',
  hoursWorked:
    'Votre logiciel de paie : le total des heures travaillées de l’année. À défaut, l’effectif en équivalent temps plein multiplié par 1 607 heures, la durée annuelle d’un temps plein.',
  collectiveBargainingPct:
    'La convention collective applicable figure sur chaque bulletin de paie (numéro IDCC). Si elle couvre tous vos salariés, indiquez 100 %.',
  trainingHoursPerEmployee:
    'Le total des heures de formation de l’année (attestations de formation, plan de développement des compétences, déclarations à votre OPCO), divisé par l’effectif.',
  genderPayGapPct:
    'À partir de 50 salariés, l’indicateur « écart de rémunération » de votre index Egapro donne cet écart.',
}
