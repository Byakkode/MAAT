import { Check, X } from 'lucide-react'
import { Link } from 'react-router-dom'
import { buttonLinkClass } from '../../components/ui/buttonStyles'
import { Container, Reveal, SectionLabel } from '../primitives'

// Offres commerciales. Enterprise est présentée en « bientôt disponible » : aucun lien
// d'inscription tant qu'elle n'est pas ouverte.
type PlanId = 'starter' | 'essential' | 'professional' | 'enterprise'

interface Plan {
  id: PlanId
  name: string
  price: string
  period: string
  yearly?: string
  tagline: string
  popular?: boolean
  comingSoon?: boolean
}

const PLANS: Plan[] = [
  {
    id: 'starter',
    name: 'Starter',
    price: '0 €',
    period: '/ mois, à vie',
    tagline: 'Pour découvrir votre niveau RSE avant de vous engager.',
  },
  {
    id: 'essential',
    name: 'Essential',
    price: '149 €',
    period: 'HT / mois',
    yearly: 'ou 1 490 € HT / an (−17 %)',
    tagline: 'Pour répondre à vos clients et structurer votre démarche RSE.',
    popular: true,
  },
  {
    id: 'professional',
    name: 'Professional',
    price: '399 €',
    period: 'HT / mois',
    yearly: 'ou 3 990 € HT / an (−17 %)',
    tagline: 'Pour les ETI et PME avancées, avec équipe RSE et obligations de reporting.',
  },
  {
    id: 'enterprise',
    name: 'Enterprise',
    price: '899 €',
    period: 'HT / mois',
    yearly: 'ou 8 990 € HT / an (−17 %)',
    tagline: 'Pour les ETI, groupes de PME et cabinets de conseil, en marque blanche.',
    comingSoon: true,
  },
]

// Une cellule vaut « inclus » ou « non inclus » ; la note précise la limite quand l'offre
// n'inclut qu'une partie de la fonctionnalité (ex. une seule évaluation en Starter).
type Cell = boolean | { note: string }

interface Feature {
  label: string
  cells: Record<PlanId, Cell>
}

const GROUPS: { title: string; features: Feature[] }[] = [
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
      { label: 'Score par domaine (E, S, G)', cells: { starter: false, essential: true, professional: true, enterprise: true } },
      {
        label: 'Recommandations',
        cells: {
          starter: { note: '3 génériques' },
          essential: { note: '12 personnalisées par secteur' },
          professional: { note: '12 personnalisées par secteur' },
          enterprise: { note: '12 personnalisées par secteur' },
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
        cells: { starter: { note: 'Basique' }, essential: { note: 'À votre logo' }, professional: { note: 'À votre logo' }, enterprise: { note: 'À votre logo' } },
      },
      { label: 'Générateur de rapport VSME', cells: { starter: false, essential: true, professional: true, enterprise: true } },
      { label: 'Préparation des questionnaires EcoVadis et B Corp', cells: { starter: false, essential: true, professional: true, enterprise: true } },
      { label: 'Module CSRD : double matérialité simplifiée', cells: { starter: false, essential: false, professional: true, enterprise: true } },
      { label: 'Reporting multi-référentiels (ESRS, GRI, ISO 26000, ODD)', cells: { starter: false, essential: false, professional: true, enterprise: true } },
      { label: 'Préparation des questionnaires CDP et SFDR', cells: { starter: false, essential: false, professional: true, enterprise: true } },
      { label: 'Alertes de conformité réglementaire (veille ESRS, VSME)', cells: { starter: false, essential: false, professional: true, enterprise: true } },
      { label: 'Rapport de durabilité audit-ready (piste de vérification)', cells: { starter: false, essential: false, professional: false, enterprise: true } },
    ],
  },
  {
    title: 'Pilotage et équipe',
    features: [
      { label: 'Suivi des actions avec indicateurs et historique', cells: { starter: false, essential: false, professional: true, enterprise: true } },
      {
        label: 'Collecte collaborative multi-contributeurs',
        cells: { starter: false, essential: false, professional: { note: '5 utilisateurs' }, enterprise: { note: 'Illimités, rôles avancés' } },
      },
      {
        label: 'API d’intégration',
        cells: { starter: false, essential: false, professional: { note: 'Export vers votre ERP' }, enterprise: { note: 'Complète : ERP, CRM, outils tiers' } },
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
      { label: 'Support par e-mail sous 48 h et base documentaire RSE', cells: { starter: false, essential: true, professional: true, enterprise: true } },
      { label: 'Support prioritaire sous 24 h et onboarding dédié de 2 h', cells: { starter: false, essential: false, professional: true, enterprise: true } },
      { label: 'Disponibilité garantie 99,9 % et responsable de compte dédié', cells: { starter: false, essential: false, professional: false, enterprise: true } },
      { label: 'Formation des équipes RSE', cells: { starter: false, essential: false, professional: false, enterprise: { note: '3 sessions par an' } } },
      { label: 'Accompagnement à la certification AFNOR Engagé RSE', cells: { starter: false, essential: false, professional: false, enterprise: true } },
    ],
  },
]

function CellContent({ cell }: { cell: Cell }) {
  const included = cell !== false
  return (
    <div className="flex flex-col items-center gap-1">
      {/* green-maat-text et non green-maat : l'icône doit atteindre 3:1 sur fond blanc (WCAG 1.4.11). */}
      {included ? (
        <Check size={20} strokeWidth={2.25} className="text-green-maat-text" aria-hidden="true" />
      ) : (
        <X size={20} strokeWidth={2.25} className="text-red" aria-hidden="true" />
      )}
      <span className="sr-only">{included ? 'Inclus' : 'Non inclus'}</span>
      {typeof cell === 'object' && <span className="text-[12.5px] leading-snug text-text-muted">{cell.note}</span>}
    </div>
  )
}

// Colonne mise en avant : même fond sur l'en-tête et sur chaque cellule, pour qu'elle se lise
// comme un bandeau vertical continu.
const highlight = (plan: Plan) => (plan.popular ? 'bg-kpi-blue' : '')

export function Pricing() {
  return (
    <section id="tarifs" aria-labelledby="tarifs-title" className="bg-bg py-24 sm:py-32">
      <Container>
        <Reveal>
          <SectionLabel index="05">Tarifs</SectionLabel>
          <h2 id="tarifs-title" className="display mt-6 max-w-[22ch] text-[clamp(2.25rem,5vw,4rem)] text-text">
            Commencez gratuitement. <span className="text-text-muted">Évoluez quand vous êtes prêts.</span>
          </h2>
        </Reveal>

        {/* Défilement horizontal sur mobile : la région est focalisable pour être parcourue au clavier. */}
        <div
          role="region"
          aria-label="Comparatif des offres"
          tabIndex={0}
          className="mt-16 overflow-x-auto rounded-card border border-border bg-white"
        >
          <table className="w-full min-w-[980px] border-collapse text-left">
            <caption className="sr-only">Fonctionnalités incluses dans chaque offre MAAT</caption>
            <thead>
              <tr>
                <td className="w-[24%] p-5" />
                {PLANS.map((plan) => (
                  <th key={plan.id} scope="col" className={`w-[19%] border-l border-border p-5 align-top font-normal ${highlight(plan)}`}>
                    <p className="font-heading text-[18px] font-semibold text-text">{plan.name}</p>
                    {/* Ligne de badge réservée dans toutes les colonnes : prix et boutons restent alignés. */}
                    <div className="mt-2 h-6">
                      {plan.popular && (
                        <span className="rounded-full bg-blue-maat px-2.5 py-0.5 text-[11.5px] font-medium text-white">Le plus choisi</span>
                      )}
                      {plan.comingSoon && (
                        <span className="rounded-full bg-kpi-amber px-2.5 py-0.5 text-[11.5px] font-medium text-amber">Bientôt disponible</span>
                      )}
                    </div>
                    <p className="mt-3 text-text">
                      <span className="numeric font-heading text-[32px] font-bold tracking-tight">{plan.price}</span>{' '}
                      <span className="text-[14px] text-text-muted">{plan.period}</span>
                    </p>
                    <p className="numeric mt-1 min-h-5 text-[13px] text-text-muted">{plan.yearly}</p>
                    <p className="mt-4 min-h-[6.5em] text-[14px] leading-relaxed text-text-muted">{plan.tagline}</p>
                    <div className="mt-5">
                      {plan.comingSoon ? (
                        <span className="inline-flex w-full items-center justify-center rounded-button border border-border px-3 py-2 font-heading text-[13px] whitespace-nowrap font-medium text-text-muted">
                          Bientôt disponible
                        </span>
                      ) : (
                        <Link
                          to="/register"
                          className={`${buttonLinkClass(plan.popular ? 'primary' : 'secondary', 'md')} w-full px-3 font-heading text-[13px] whitespace-nowrap`}
                        >
                          {plan.id === 'starter' ? 'Commencer gratuitement' : `Choisir ${plan.name}`}
                        </Link>
                      )}
                    </div>
                  </th>
                ))}
              </tr>
            </thead>
            {GROUPS.map((group) => (
              <tbody key={group.title}>
                <tr className="border-t border-border">
                  <th scope="rowgroup" colSpan={5} className="bg-bg/60 px-6 py-3 font-heading text-[13px] font-semibold tracking-wide text-text uppercase">
                    {group.title}
                  </th>
                </tr>
                {group.features.map((feature) => (
                  <tr key={feature.label} className="border-t border-border">
                    <th scope="row" className="px-6 py-4 text-[14.5px] font-normal text-text">
                      {feature.label}
                    </th>
                    {PLANS.map((plan) => (
                      <td key={plan.id} className={`border-l border-border px-4 py-4 text-center ${highlight(plan)}`}>
                        <CellContent cell={feature.cells[plan.id]} />
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            ))}
          </table>
        </div>

        <p className="mt-6 text-[13px] text-text-muted">Tous les prix sont indiqués hors taxes (HT).</p>
      </Container>
    </section>
  )
}
