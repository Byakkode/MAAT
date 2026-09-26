import { useEffect, useState } from 'react'
import { Navigate, Outlet } from 'react-router-dom'
import { needsPlanChoice, useSubscriptionStore } from '../store/subscriptionStore'

// docs/specs/abonnement.md, section 2 : entre ProtectedRoute (session) et AppShell (coquille).
// Une entreprise sans offre, ou dont l'offre payante attend son paiement, est envoyée sur
// l'écran de sélection avant tout autre écran de l'application.
//
// Rechargé à chaque montage, c'est-à-dire à chaque ouverture de session : jamais l'état
// d'une session précédente (autre compte, autre entreprise).
//
// En cas d'erreur de chargement, l'application reste accessible : toutes les fonctionnalités
// sont ouvertes quelle que soit l'offre, bloquer l'accès sur une panne de l'API de facturation
// ne protégerait rien.
export function SubscriptionGate() {
  const subscription = useSubscriptionStore((state) => state.subscription)
  const load = useSubscriptionStore((state) => state.load)
  // Drapeau local plutôt que le statut du store : au premier rendu, le store peut encore
  // porter l'état chargé par l'écran précédent (« loaded ») — l'afficher puis basculer en
  // chargement démonterait la coquille aussitôt montée.
  const [checked, setChecked] = useState(false)

  useEffect(() => {
    let active = true
    load().finally(() => {
      if (active) setChecked(true)
    })
    return () => {
      active = false
    }
  }, [load])

  if (!checked) {
    return (
      <p role="status" className="text-text-muted">
        Chargement de votre abonnement…
      </p>
    )
  }

  if (needsPlanChoice(subscription)) {
    return <Navigate to="/abonnement" replace />
  }

  return <Outlet />
}
