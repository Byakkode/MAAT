import { AlertTriangle } from 'lucide-react'
import { useCurrentUserStore } from '../../store/currentUserStore'

// docs/specs/coquille-et-compte.md, section 4 : "Un bandeau si l'adresse n'est pas vérifiée,
// sous l'en-tête, sur tous les écrans, avec un bouton de renvoi [...] Le bandeau se ferme pour
// la session, jamais définitivement." — bannerDismissed vit en mémoire (currentUserStore), pas
// en stockage persistant, donc il réapparaît de lui-même à la prochaine visite tant que
// l'adresse reste non vérifiée.
export function VerificationBanner() {
  const emailVerified = useCurrentUserStore((s) => s.emailVerified)
  const bannerDismissed = useCurrentUserStore((s) => s.bannerDismissed)
  const dismissBanner = useCurrentUserStore((s) => s.dismissBanner)
  const resendStatus = useCurrentUserStore((s) => s.resendStatus)
  const resendError = useCurrentUserStore((s) => s.resendError)
  const resendVerificationEmail = useCurrentUserStore((s) => s.resendVerificationEmail)

  if (emailVerified || bannerDismissed) {
    return null
  }

  return (
    <div role="status" className="flex flex-wrap items-center justify-between gap-3 border-b border-amber bg-amber/10 px-4 py-3">
      <div className="flex items-center gap-2">
        <AlertTriangle size={20} strokeWidth={1.5} aria-hidden="true" className="shrink-0 text-amber" />
        <p className="text-sm text-text">
          Votre adresse e-mail n&apos;est pas vérifiée. Le rapport PDF reste inaccessible tant qu&apos;elle ne l&apos;est pas.
          {resendStatus === 'sent' && ' Un nouvel e-mail de vérification vient d’être envoyé.'}
          {resendStatus === 'error' && resendError && ` ${resendError}`}
        </p>
      </div>
      <div className="flex shrink-0 items-center gap-3">
        <button
          type="button"
          onClick={() => void resendVerificationEmail()}
          disabled={resendStatus === 'sending'}
          className="rounded-button border border-amber px-3 py-1 text-sm font-medium text-amber disabled:opacity-50"
        >
          {resendStatus === 'sending' ? 'Envoi…' : 'Renvoyer l’e-mail'}
        </button>
        <button type="button" onClick={dismissBanner} className="text-sm font-medium text-text-muted" aria-label="Fermer ce message">
          Fermer
        </button>
      </div>
    </div>
  )
}
