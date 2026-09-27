import { AlertTriangle } from 'lucide-react'
import { Link } from 'react-router-dom'
import { useSubscriptionStore } from '../../store/subscriptionStore'

// docs/specs/abonnement.md, section 8 : pendant un impayé, l'offre reste ouverte (Stripe
// relance le paiement) mais l'entreprise doit le savoir, sur tous les écrans. Non refermable :
// sans régularisation, l'abonnement finit par s'arrêter et l'entreprise repasse sur Starter.
export function PastDueBanner() {
  const pastDue = useSubscriptionStore((s) => s.subscription?.status === 'PastDue')

  if (!pastDue) {
    return null
  }

  return (
    <div role="status" className="flex flex-wrap items-center gap-2 border-b border-amber bg-amber/10 px-4 py-3">
      <AlertTriangle size={20} strokeWidth={1.5} aria-hidden="true" className="shrink-0 text-amber" />
      <p className="text-sm text-text">
        Le dernier paiement de votre abonnement a échoué. Votre offre reste active pendant les
        relances : mettez à jour votre moyen de paiement depuis{' '}
        <Link to="/compte" className="font-medium text-blue-maat-text hover:underline">
          votre compte
        </Link>{' '}
        pour ne pas repasser sur l&apos;offre Starter.
      </p>
    </div>
  )
}
