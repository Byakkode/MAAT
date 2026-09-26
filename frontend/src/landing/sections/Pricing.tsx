import { useState } from 'react'
import { Link } from 'react-router-dom'
import { buttonLinkClass } from '../../components/ui/buttonStyles'
import { BillingPeriodToggle } from '../../billing/BillingPeriodToggle'
import { PLAN_ACTION_CLASS, PlanActionPlaceholder, PlanComparisonTable } from '../../billing/PlanComparisonTable'
import { registerPath, type BillingPeriodChoice } from '../../billing/plans'
import { Container, Reveal, SectionLabel } from '../primitives'

// L'offre et la période choisies ici suivent le visiteur jusqu'à l'inscription
// (docs/specs/abonnement.md, section 2) : il n'a pas à les choisir une seconde fois.
export function Pricing() {
  const [period, setPeriod] = useState<BillingPeriodChoice>('monthly')

  return (
    <section id="tarifs" aria-labelledby="tarifs-title" className="bg-bg py-24 sm:py-32">
      <Container>
        <Reveal>
          <SectionLabel index="05">Tarifs</SectionLabel>
          <h2 id="tarifs-title" className="display mt-6 max-w-[22ch] text-[clamp(2.25rem,5vw,4rem)] text-text">
            Commencez gratuitement. <span className="text-text-muted">Évoluez quand vous êtes prêts.</span>
          </h2>
        </Reveal>

        <div className="mt-12 mb-6">
          <BillingPeriodToggle value={period} onChange={setPeriod} />
        </div>

        <PlanComparisonTable
          period={period}
          renderAction={(plan) =>
            plan.comingSoon ? (
              <PlanActionPlaceholder>Bientôt disponible</PlanActionPlaceholder>
            ) : (
              <Link
                to={registerPath(plan.id, period)}
                className={`${buttonLinkClass(plan.popular ? 'primary' : 'secondary', 'md')} ${PLAN_ACTION_CLASS}`}
              >
                {plan.id === 'starter' ? 'Commencer gratuitement' : `Choisir ${plan.name}`}
              </Link>
            )
          }
        />

        <p className="mt-6 text-[13px] text-text-muted">Tous les prix sont indiqués hors taxes (HT).</p>
      </Container>
    </section>
  )
}
