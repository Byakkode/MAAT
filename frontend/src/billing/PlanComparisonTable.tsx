import type { ReactNode } from 'react'
import { Check, X } from 'lucide-react'
import { FEATURE_GROUPS, PLANS, type BillingPeriodChoice, type Cell, type Plan } from './plans'

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

interface PlanComparisonTableProps {
  period: BillingPeriodChoice
  // Bouton ou lien sous le prix de chaque offre : lien d'inscription sur la page d'accueil,
  // choix de l'offre dans l'application.
  renderAction: (plan: Plan) => ReactNode
}

// Comparatif des offres, offres en colonnes et options en lignes. Partagé par la section Tarifs
// de la page d'accueil et l'écran de sélection d'abonnement (docs/specs/abonnement.md, section 1).
export function PlanComparisonTable({ period, renderAction }: PlanComparisonTableProps) {
  return (
    // Défilement horizontal sur mobile : la région est focalisable pour être parcourue au clavier.
    <div
      role="region"
      aria-label="Comparatif des offres"
      tabIndex={0}
      className="overflow-x-auto rounded-card border border-border bg-white"
    >
      <table className="w-full min-w-[980px] border-collapse text-left">
        <caption className="sr-only">Fonctionnalités incluses dans chaque offre MAAT</caption>
        <thead>
          <tr>
            <td className="w-[24%] p-5" />
            {PLANS.map((plan) => {
              const price = plan.prices[period]
              return (
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
                    <span className="numeric font-heading text-[32px] font-bold tracking-tight">{price.amount}</span>{' '}
                    <span className="text-[14px] text-text-muted">{price.unit}</span>
                  </p>
                  <p className="numeric mt-1 min-h-5 text-[13px] text-text-muted">{price.detail}</p>
                  <p className="mt-4 min-h-[6.5em] text-[14px] leading-relaxed text-text-muted">{plan.tagline}</p>
                  <div className="mt-5">{renderAction(plan)}</div>
                </th>
              )
            })}
          </tr>
        </thead>
        {FEATURE_GROUPS.map((group) => (
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
  )
}

// Classes communes aux actions des colonnes (lien ou bouton) : même largeur, une seule ligne,
// pour que les quatre colonnes restent alignées.
export const PLAN_ACTION_CLASS = 'w-full px-3 font-heading text-[13px] whitespace-nowrap'

// Action inerte d'une offre non souscriptible (Enterprise), ou de l'offre déjà en cours.
export function PlanActionPlaceholder({ children }: { children: ReactNode }) {
  return (
    <span className="inline-flex w-full items-center justify-center rounded-button border border-border px-3 py-2 font-heading text-[13px] font-medium whitespace-nowrap text-text-muted">
      {children}
    </span>
  )
}
