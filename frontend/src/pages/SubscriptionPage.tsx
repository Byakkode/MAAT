import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import * as billingApi from '../api/billingApi'
import { fromApiPlan, type Subscription } from '../api/billingApi'
import { BillingPeriodToggle } from '../billing/BillingPeriodToggle'
import { PLAN_ACTION_CLASS, PlanActionPlaceholder, PlanComparisonTable } from '../billing/PlanComparisonTable'
import { findPlan, type BillingPeriodChoice, type Plan, type PlanId } from '../billing/plans'
import { Button } from '../components/ui/Button'
import { LogoHorizontal } from '../components/ui/Logo'
import { Container } from '../landing/primitives'
import { useAuthStore } from '../store/authStore'
import { needsPlanChoice, useSubscriptionStore } from '../store/subscriptionStore'

// Offre payante choisie sur la page d'accueil, pas encore payée : l'écran de sélection est
// sauté et l'utilisateur part directement vers le paiement (docs/specs/abonnement.md,
// section 2). Sauf retour d'un paiement annulé, sans quoi il y repartirait en boucle.
function shouldResumeCheckout(subscription: Subscription | null, canceled: boolean): boolean {
  return !canceled && subscription?.status === 'PendingPayment' && subscription.plan !== null
}

// docs/specs/abonnement.md, section 2 : écran de sélection, hors de la coquille de
// l'application (SubscriptionGate y renvoie tant qu'aucune offre n'est choisie).
export function SubscriptionPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const canceled = searchParams.get('paiement') === 'annule'
  const isAdmin = useAuthStore((state) => state.user?.role === 'Admin')
  const logout = useAuthStore((state) => state.logout)
  const subscription = useSubscriptionStore((state) => state.subscription)
  const loadStatus = useSubscriptionStore((state) => state.status)
  const load = useSubscriptionStore((state) => state.load)
  const setSubscription = useSubscriptionStore((state) => state.set)

  const [loaded, setLoaded] = useState(false)
  const [period, setPeriod] = useState<BillingPeriodChoice>('monthly')
  const [pendingPlan, setPendingPlan] = useState<PlanId | 'portal' | null>(null)
  const [error, setError] = useState<string | null>(null)
  // StrictMode monte les effets deux fois en développement : sans ce verrou, deux sessions de
  // paiement seraient ouvertes pour une seule arrivée sur la page.
  const resumeStarted = useRef(false)

  useEffect(() => {
    load().then((current) => {
      if (current?.billingPeriod === 'Yearly') setPeriod('yearly')
      setLoaded(true)
    })
  }, [load])

  // Ne dépend que de setters (stables) : l'effet de reprise ci-dessous peut l'appeler sans se
  // redéclencher.
  const goToCheckout = useCallback(async (plan: PlanId, chosenPeriod: BillingPeriodChoice) => {
    setError(null)
    setPendingPlan(plan)
    try {
      billingApi.redirectTo(await billingApi.startCheckout(plan, chosenPeriod))
    } catch (err) {
      setError(err instanceof Error ? err.message : "Impossible d'ouvrir la page de paiement.")
      setPendingPlan(null)
    }
  }, [])

  useEffect(() => {
    if (!loaded || !isAdmin || resumeStarted.current || !shouldResumeCheckout(subscription, canceled)) return
    resumeStarted.current = true
    void goToCheckout(fromApiPlan(subscription!.plan!), subscription!.billingPeriod === 'Yearly' ? 'yearly' : 'monthly')
  }, [loaded, isAdmin, subscription, canceled, goToCheckout])

  async function goToPortal() {
    setError(null)
    setPendingPlan('portal')
    try {
      billingApi.redirectTo(await billingApi.openCustomerPortal())
    } catch (err) {
      setError(err instanceof Error ? err.message : "Impossible d'ouvrir la gestion de l'abonnement.")
      setPendingPlan(null)
    }
  }

  async function chooseStarter() {
    setError(null)
    setPendingPlan('starter')
    const firstChoice = needsPlanChoice(subscription)
    try {
      setSubscription(await billingApi.chooseStarter())
      // Premier choix d'offre juste après la connexion : l'animation d'arrivée (LoginIntro)
      // n'a pas encore été jouée, SubscriptionGate ayant redirigé ici avant la coquille.
      navigate('/tableau-de-bord', { state: firstChoice ? { loginIntro: true } : undefined })
    } catch (err) {
      setError(err instanceof Error ? err.message : "Impossible de choisir l'offre Starter.")
      setPendingPlan(null)
    }
  }

  async function handleLogout() {
    await logout()
    navigate('/login')
  }

  const currentPlan = subscription?.plan && subscription.status !== 'PendingPayment' ? fromApiPlan(subscription.plan) : null
  const hasPaidPlan = currentPlan !== null && currentPlan !== 'starter' && subscription?.hasBillingAccount === true

  function renderAction(plan: Plan) {
    if (plan.comingSoon) return <PlanActionPlaceholder>Bientôt disponible</PlanActionPlaceholder>
    if (plan.id === currentPlan) return <PlanActionPlaceholder>Offre actuelle</PlanActionPlaceholder>
    if (!isAdmin) return null

    // Un abonnement payant en cours se modifie depuis le portail Stripe (changement d'offre,
    // résiliation), jamais par un second paiement.
    if (hasPaidPlan) {
      return (
        <Button variant="secondary" className={PLAN_ACTION_CLASS} isLoading={pendingPlan === 'portal'} disabled={pendingPlan !== null} onClick={goToPortal}>
          Changer d&apos;offre
        </Button>
      )
    }

    if (plan.id === 'starter') {
      return (
        <Button variant="secondary" className={PLAN_ACTION_CLASS} isLoading={pendingPlan === 'starter'} disabled={pendingPlan !== null} onClick={chooseStarter}>
          Commencer gratuitement
        </Button>
      )
    }

    return (
      <Button
        variant={plan.popular ? 'primary' : 'secondary'}
        className={PLAN_ACTION_CLASS}
        isLoading={pendingPlan === plan.id}
        disabled={pendingPlan !== null}
        onClick={() => goToCheckout(plan.id, period)}
      >
        Choisir {plan.name}
      </Button>
    )
  }

  const resuming = loaded && isAdmin && shouldResumeCheckout(subscription, canceled) && error === null

  return (
    <div className="landing min-h-screen bg-bg font-body text-text">
      <header className="border-b border-border bg-white">
        <Container className="flex h-16 items-center justify-between gap-4">
          <LogoHorizontal />
          <div className="flex items-center gap-4">
            {!needsPlanChoice(subscription) && subscription && (
              <Link to="/tableau-de-bord" className="text-[14px] text-text-muted hover:text-text">
                Retour au tableau de bord
              </Link>
            )}
            <Button variant="ghost" onClick={handleLogout}>
              Se déconnecter
            </Button>
          </div>
        </Container>
      </header>

      <main>
        <Container className="py-12 sm:py-16">
          {!loaded || resuming ? (
            <p role="status" className="text-text-muted">
              {resuming
                ? `Redirection vers le paiement sécurisé de l'offre ${findPlan(fromApiPlan(subscription!.plan!)).name}…`
                : 'Chargement des offres…'}
            </p>
          ) : (
            <>
              <h1 className="display text-[clamp(2rem,4vw,3rem)] text-text">
                {currentPlan ? 'Votre abonnement' : 'Choisissez votre offre'}
              </h1>
              <p className="mt-3 max-w-[60ch] text-[15px] leading-relaxed text-text-muted">
                Commencez gratuitement avec Starter, ou choisissez une offre payante : vous pourrez en changer à tout moment.
              </p>

              {canceled && (
                <p role="status" className="mt-6 rounded-button border border-border bg-white px-4 py-3 text-[14px] text-text">
                  Paiement annulé : aucun montant n&apos;a été prélevé. Choisissez une offre pour continuer.
                </p>
              )}
              {!isAdmin && (
                <p role="status" className="mt-6 rounded-button border border-border bg-white px-4 py-3 text-[14px] text-text">
                  Seul un administrateur de votre entreprise peut choisir ou modifier l&apos;offre.
                </p>
              )}
              {error && (
                <p role="alert" className="mt-6 text-[14px] text-red">
                  {error}
                </p>
              )}
              {loadStatus === 'error' && (
                <p role="alert" className="mt-6 text-[14px] text-red">
                  Impossible de charger votre abonnement actuel.
                </p>
              )}

              <div className="mt-8 mb-6">
                <BillingPeriodToggle value={period} onChange={setPeriod} />
              </div>

              <PlanComparisonTable period={period} renderAction={renderAction} />

              <p className="mt-6 text-[13px] text-text-muted">
                Tous les prix sont indiqués hors taxes (HT). Paiement sécurisé par Stripe : vos coordonnées bancaires ne transitent jamais par MAAT.
              </p>
            </>
          )}
        </Container>
      </main>
    </div>
  )
}
