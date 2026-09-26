import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import * as billingApi from '../../api/billingApi'
import { fromApiPlan } from '../../api/billingApi'
import { findPlan } from '../../billing/plans'
import { useAuthStore } from '../../store/authStore'
import { useSubscriptionStore } from '../../store/subscriptionStore'
import { buttonLinkClass } from '../ui/buttonStyles'
import { Button } from '../ui/Button'
import { Card } from '../ui/Card'

const PERIOD_LABELS = { Monthly: 'mensuelle', Yearly: 'annuelle' } as const

// docs/specs/abonnement.md, section 2 : l'offre en cours, et l'accès à sa gestion — portail
// Stripe pour un abonnement payant (factures, moyen de paiement, changement d'offre,
// résiliation), écran de sélection pour passer de Starter à une offre payante.
export function SubscriptionCard() {
  const isAdmin = useAuthStore((state) => state.user?.role === 'Admin')
  const subscription = useSubscriptionStore((state) => state.subscription)
  const setSubscription = useSubscriptionStore((state) => state.set)
  const load = useSubscriptionStore((state) => state.load)
  const [opening, setOpening] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Le portail Stripe renvoie ici (docs/specs/abonnement.md, section 2) : l'abonnement est relu
  // chez Stripe à l'affichage, pour qu'un changement d'offre ou une résiliation qui vient d'y
  // être faite s'affiche aussitôt, sans attendre le webhook. Sans abonnement payant, l'API
  // répond sans interroger Stripe.
  useEffect(() => {
    billingApi.refreshSubscription().then(setSubscription, () => void load())
  }, [setSubscription, load])

  async function openPortal() {
    setError(null)
    setOpening(true)
    try {
      billingApi.redirectTo(await billingApi.openCustomerPortal())
    } catch (err) {
      setError(err instanceof Error ? err.message : "Impossible d'ouvrir la gestion de l'abonnement.")
      setOpening(false)
    }
  }

  const plan = subscription?.plan ? findPlan(fromApiPlan(subscription.plan)) : null
  const isPaid = plan !== null && plan.id !== 'starter'

  return (
    <Card as="section" aria-labelledby="subscription-heading">
      <h2 id="subscription-heading" className="text-[15px] font-semibold text-text">
        Abonnement
      </h2>

      {plan ? (
        <p className="mt-2 text-[14px] text-text-muted">
          Offre <span className="font-medium text-text">{plan.name}</span>
          {isPaid && subscription?.billingPeriod && <>, facturation {PERIOD_LABELS[subscription.billingPeriod]}</>}
        </p>
      ) : (
        <p className="mt-2 text-[14px] text-text-muted">Aucune offre choisie.</p>
      )}

      {subscription?.status === 'PastDue' && (
        <p role="alert" className="mt-3 rounded-button border border-amber bg-amber/10 px-3 py-2 text-[13px] text-amber">
          Le dernier paiement a échoué. Mettez à jour votre moyen de paiement pour conserver votre offre.
        </p>
      )}

      {error && (
        <p role="alert" className="mt-3 text-[13px] text-red">
          {error}
        </p>
      )}

      {isAdmin && (
        <div className="mt-4 flex flex-wrap gap-3">
          {subscription?.hasBillingAccount && (
            <Button variant="secondary" isLoading={opening} onClick={openPortal}>
              Gérer mon abonnement
            </Button>
          )}
          {!isPaid && (
            <Link to="/abonnement" className={buttonLinkClass('primary', 'md')}>
              Voir les offres
            </Link>
          )}
        </div>
      )}
    </Card>
  )
}
