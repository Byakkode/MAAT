// Offres commerciales, partagées par la page d'accueil (section Tarifs) et l'écran de sélection
// de l'application (docs/specs/abonnement.md, section 1) : un seul endroit à modifier quand un
// prix ou une option change. Enterprise est présentée en « bientôt disponible » : ni lien
// d'inscription ni paiement tant qu'elle n'est pas ouverte.
export type PlanId = 'starter' | 'essential' | 'professional' | 'enterprise'

export type BillingPeriodChoice = 'monthly' | 'yearly'

interface PriceLine {
  // Montant mis en avant, puis unité et ligne secondaire en petit.
  amount: string
  unit: string
  detail?: string
}

export interface Plan {
  id: PlanId
  name: string
  prices: Record<BillingPeriodChoice, PriceLine>
  tagline: string
  popular?: boolean
  comingSoon?: boolean
}

// Starter est gratuit à vie : même affichage quelle que soit la période choisie.
const FREE: PriceLine = { amount: '0 €', unit: '/ mois, à vie' }

export const PLANS: Plan[] = [
  {
    id: 'starter',
    name: 'Starter',
    prices: { monthly: FREE, yearly: FREE },
    tagline: 'Pour découvrir votre niveau RSE avant de vous engager.',
  },
  {
    id: 'essential',
    name: 'Essential',
    prices: {
      monthly: { amount: '149 €', unit: 'HT / mois', detail: 'ou 1 490 € HT / an (−17 %)' },
      yearly: { amount: '1 490 €', unit: 'HT / an', detail: 'soit 2 mois offerts' },
    },
    tagline: 'Pour répondre à vos clients et structurer votre démarche RSE.',
    popular: true,
  },
  {
    id: 'professional',
    name: 'Professional',
    prices: {
      monthly: { amount: '399 €', unit: 'HT / mois', detail: 'ou 3 990 € HT / an (−17 %)' },
      yearly: { amount: '3 990 €', unit: 'HT / an', detail: 'soit 2 mois offerts' },
    },
    tagline: 'Pour les ETI et PME avancées, avec équipe RSE et obligations de reporting.',
  },
  {
    id: 'enterprise',
    name: 'Enterprise',
    prices: {
      monthly: { amount: '899 €', unit: 'HT / mois', detail: 'ou 8 990 € HT / an (−17 %)' },
      yearly: { amount: '8 990 €', unit: 'HT / an', detail: 'soit 2 mois offerts' },
    },
    tagline: 'Pour les ETI, groupes de PME et cabinets de conseil, en marque blanche.',
    comingSoon: true,
  },
]

export function findPlan(id: PlanId): Plan {
  return PLANS.find((plan) => plan.id === id)!
}

// Paramètres d'URL de l'inscription (« /register?offre=essential&periode=annuelle ») :
// transmis depuis la page d'accueil, relus par RegisterPage. En français, comme les routes.
const PERIOD_PARAMS: Record<BillingPeriodChoice, string> = { monthly: 'mensuelle', yearly: 'annuelle' }

export function registerPath(plan: PlanId, period: BillingPeriodChoice): string {
  const params = new URLSearchParams({ offre: plan })
  if (plan !== 'starter') params.set('periode', PERIOD_PARAMS[period])
  return `/register?${params.toString()}`
}

// Offre lue depuis l'URL : null si absente, inconnue ou pas encore ouverte (Enterprise).
export function parsePlanParams(params: URLSearchParams): { plan: PlanId; period: BillingPeriodChoice } | null {
  const plan = PLANS.find((p) => p.id === params.get('offre') && !p.comingSoon)
  if (!plan) return null
  const period: BillingPeriodChoice = params.get('periode') === PERIOD_PARAMS.yearly ? 'yearly' : 'monthly'
  return { plan: plan.id, period }
}

// Une cellule vaut « inclus » ou « non inclus » ; la note précise la limite quand l'offre
// n'inclut qu'une partie de la fonctionnalité (ex. une seule évaluation en Starter).
export type Cell = boolean | { note: string }

// comingSoon : annoncée mais pas encore construite (docs/specs/abonnement.md, section 8). La
// ligne reste visible, marquée « Bientôt », et perd cette marque dans le commit qui la livre.
// Les options propres à Enterprise n'en portent pas : toute la colonne est déjà annoncée
// « Bientôt disponible ».
export interface Feature {
  label: string
  cells: Record<PlanId, Cell>
  comingSoon?: boolean
}

export const FEATURE_GROUPS: { title: string; features: Feature[] }[] = [
  {
    title: 'Diagnostic et score',
    features: [
      {
        label: 'Diagnostic RSE (questionnaire de 30 min)',
        cells: {
          starter: { note: '1 seule évaluation' },
          essential: { note: 'Illimité, relançable chaque trimestre' },
          professional: { note: 'Illimité' },
          enterprise: { note: 'Illimité' },
        },
      },
      { label: 'Score RSE global sur 100', cells: { starter: true, essential: true, professional: true, enterprise: true } },
      { label: 'Score par domaine (5 domaines RSE)', cells: { starter: false, essential: true, professional: true, enterprise: true } },
      {
        label: 'Recommandations',
        cells: {
          starter: { note: '3 prioritaires' },
          essential: { note: '12, priorisées selon votre secteur' },
          professional: { note: 'Toutes, priorisées selon votre secteur' },
          enterprise: { note: 'Toutes, priorisées selon votre secteur' },
        },
      },
      {
        label: 'Tableau de bord',
        cells: {
          starter: { note: 'Lecture seule' },
          essential: { note: 'Interactif, suivi de progression' },
          professional: { note: 'Interactif, suivi de progression' },
          enterprise: { note: 'Interactif, suivi de progression' },
        },
      },
    ],
  },
  {
    title: 'Rapports et conformité',
    features: [
      {
        label: 'Rapport PDF',
        cells: {
          starter: { note: 'Score global' },
          essential: { note: 'Complet : domaines, évolution, plan d’actions' },
          professional: { note: 'Complet, avec vos indicateurs' },
          enterprise: { note: 'Complet, avec vos indicateurs' },
        },
      },
      { label: 'Logo de votre entreprise sur le rapport', cells: { starter: false, essential: true, professional: true, enterprise: true }, comingSoon: true },
      { label: 'Générateur de rapport VSME', cells: { starter: false, essential: true, professional: true, enterprise: true }, comingSoon: true },
      { label: 'Préparation des questionnaires EcoVadis et B Corp', cells: { starter: false, essential: true, professional: true, enterprise: true }, comingSoon: true },
      { label: 'Module CSRD : double matérialité simplifiée', cells: { starter: false, essential: false, professional: true, enterprise: true }, comingSoon: true },
      { label: 'Reporting multi-référentiels (ESRS, GRI, ISO 26000, ODD)', cells: { starter: false, essential: false, professional: true, enterprise: true }, comingSoon: true },
      { label: 'Préparation des questionnaires CDP et SFDR', cells: { starter: false, essential: false, professional: true, enterprise: true }, comingSoon: true },
      { label: 'Alertes de conformité réglementaire (veille ESRS, VSME)', cells: { starter: false, essential: false, professional: true, enterprise: true }, comingSoon: true },
      { label: 'Rapport de durabilité audit-ready (piste de vérification)', cells: { starter: false, essential: false, professional: false, enterprise: true } },
    ],
  },
  {
    title: 'Pilotage et équipe',
    features: [
      {
        label: 'Suivi des actions : statut, responsable, échéance, notes',
        cells: { starter: false, essential: { note: 'Actions terminées' }, professional: true, enterprise: true },
      },
      { label: 'Indicateurs RSE chiffrés, suivis d’une année sur l’autre', cells: { starter: false, essential: false, professional: true, enterprise: true } },
      { label: 'Historique du suivi des actions', cells: { starter: false, essential: false, professional: true, enterprise: true }, comingSoon: true },
      {
        label: 'Collecte collaborative multi-contributeurs',
        cells: { starter: false, essential: false, professional: { note: '5 utilisateurs' }, enterprise: { note: 'Illimités, rôles avancés' } },
        comingSoon: true,
      },
      {
        label: 'API d’intégration',
        cells: { starter: false, essential: false, professional: { note: 'Export vers votre ERP' }, enterprise: { note: 'Complète : ERP, CRM, outils tiers' } },
        comingSoon: true,
      },
      {
        label: 'Benchmark sectoriel',
        cells: { starter: false, essential: false, professional: { note: 'Anonymisé' }, enterprise: { note: 'Complet, par secteur et région' } },
      },
      { label: 'Marque blanche (logo du consultant ou du cabinet)', cells: { starter: false, essential: false, professional: false, enterprise: true } },
      { label: 'Programme partenaire consultants (suivi des commissions)', cells: { starter: false, essential: false, professional: false, enterprise: true } },
    ],
  },
  {
    title: 'Accompagnement',
    features: [
      { label: 'Support par ticket, réponse sous 48 h', cells: { starter: false, essential: true, professional: true, enterprise: true } },
      { label: 'Base documentaire RSE', cells: { starter: false, essential: true, professional: true, enterprise: true }, comingSoon: true },
      { label: 'Support prioritaire sous 24 h et onboarding dédié de 2 h', cells: { starter: false, essential: false, professional: true, enterprise: true } },
      { label: 'Disponibilité garantie 99,9 % et responsable de compte dédié', cells: { starter: false, essential: false, professional: false, enterprise: true } },
      { label: 'Formation des équipes RSE', cells: { starter: false, essential: false, professional: false, enterprise: { note: '3 sessions par an' } } },
      { label: 'Accompagnement à la certification AFNOR Engagé RSE', cells: { starter: false, essential: false, professional: false, enterprise: true } },
    ],
  },
]
