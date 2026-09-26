import { useCallback, useEffect, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import * as billingApi from '../api/billingApi'
import type { Subscription } from '../api/billingApi'
import { Button } from '../components/ui/Button'
import { LogoHorizontal } from '../components/ui/Logo'
import { Container } from '../landing/primitives'
import { useSubscriptionStore } from '../store/subscriptionStore'

// Repli quand la session n'est pas encore payée au retour (3-D Secure en cours, par exemple) :
// on attend alors le webhook, 30 s au plus.
const POLL_INTERVAL_MS = 2000
const MAX_ATTEMPTS = 15

function isPaidPlanActive(subscription: Subscription | null): boolean {
  return (
    subscription !== null &&
    subscription.plan !== null &&
    subscription.plan !== 'Starter' &&
    (subscription.status === 'Active' || subscription.status === 'PastDue')
  )
}

// docs/specs/abonnement.md, section 5 : Stripe renvoie ici avec l'identifiant de la session de
// paiement. L'API relit cette session chez Stripe et active l'offre si elle est payée — la
// page n'active jamais rien elle-même, et l'identifiant seul ne prouve rien. Le tableau de
// bord s'ouvre ensuite sans aucune action de l'utilisateur.
export function SubscriptionConfirmationPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const sessionId = searchParams.get('session_id')
  const load = useSubscriptionStore((state) => state.load)
  const setSubscription = useSubscriptionStore((state) => state.set)
  const [outcome, setOutcome] = useState<'waiting' | 'active' | 'late'>('waiting')
  const [round, setRound] = useState(0)

  const confirmThenPoll = useCallback(async (isActive: () => boolean) => {
    if (sessionId) {
      try {
        const confirmed = await billingApi.confirmCheckout(sessionId)
        setSubscription(confirmed)
        if (isPaidPlanActive(confirmed)) {
          if (isActive()) setOutcome('active')
          return
        }
      } catch {
        // Confirmation impossible (Stripe injoignable, par exemple) : le webhook prendra le relais.
      }
    }

    for (let attempt = 0; attempt < MAX_ATTEMPTS && isActive(); attempt++) {
      if (isPaidPlanActive(await load())) {
        if (isActive()) setOutcome('active')
        return
      }
      await new Promise((resolve) => setTimeout(resolve, POLL_INTERVAL_MS))
    }
    if (isActive()) setOutcome('late')
  }, [sessionId, load, setSubscription])

  useEffect(() => {
    let active = true
    setOutcome('waiting')
    void confirmThenPoll(() => active)
    return () => {
      active = false
    }
  }, [confirmThenPoll, round])

  useEffect(() => {
    if (outcome !== 'active') return
    // Court délai pour que le message de succès soit lu (et annoncé par les lecteurs d'écran)
    // avant l'ouverture du tableau de bord.
    const timer = setTimeout(() => navigate('/tableau-de-bord', { replace: true, state: { loginIntro: true } }), 1200)
    return () => clearTimeout(timer)
  }, [outcome, navigate])

  return (
    <div className="landing min-h-screen bg-bg font-body text-text">
      <header className="border-b border-border bg-white">
        <Container className="flex h-16 items-center">
          <LogoHorizontal />
        </Container>
      </header>
      <main>
        <Container className="py-16">
          <h1 className="display text-[clamp(2rem,4vw,3rem)] text-text">Confirmation de votre abonnement</h1>
          {outcome === 'waiting' && (
            <p role="status" className="mt-4 text-[15px] text-text-muted">
              Vérification de votre paiement auprès de Stripe…
            </p>
          )}
          {outcome === 'active' && (
            <p role="status" className="mt-4 text-[15px] text-green-maat-text">
              Votre abonnement est actif. Redirection vers votre tableau de bord…
            </p>
          )}
          {outcome === 'late' && (
            <div className="mt-4 flex flex-col items-start gap-4">
              <p role="status" className="max-w-[60ch] text-[15px] text-text">
                Votre paiement a bien été transmis, mais sa confirmation prend plus de temps que prévu. Aucun second paiement n&apos;est nécessaire.
              </p>
              <Button onClick={() => setRound((r) => r + 1)}>Vérifier à nouveau</Button>
            </div>
          )}
        </Container>
      </main>
    </div>
  )
}
