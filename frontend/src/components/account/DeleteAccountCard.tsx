import { type FormEvent, useState } from 'react'
import * as accountApi from '../../api/accountApi'
import { ApiError } from '../../api/authApi'
import { notifySessionExpired } from '../../api/tokenStore'
import { useCurrentUserStore } from '../../store/currentUserStore'

type Status = 'idle' | 'submitting' | 'error'

// docs/specs/coquille-et-compte.md, section 6. Vérifié contre AccountService.DeleteAccountAsync
// (docs/specs/auth-securite-rgpd.md, section 6, et coquille-et-compte.md, section 6) : la
// suppression emporte toute l'entreprise — tous ses comptes, tous ses diagnostics — quel que
// soit le rôle de l'appelant et sans distinction du nombre d'administrateurs restants. Le texte
// ci-dessous décrit ce comportement réel, pas une intention non implémentée.
export function DeleteAccountCard() {
  const email = useCurrentUserStore((s) => s.email)
  const [confirmationEmail, setConfirmationEmail] = useState('')
  const [password, setPassword] = useState('')
  const [status, setStatus] = useState<Status>('idle')
  const [error, setError] = useState<string | null>(null)

  const emailMatches = confirmationEmail !== '' && confirmationEmail.trim().toLowerCase() === (email ?? '').toLowerCase()

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!emailMatches) {
      return
    }

    setStatus('submitting')
    setError(null)
    try {
      await accountApi.deleteAccount(password)
      // Le compte et son refresh token n'existent plus côté serveur : notifySessionExpired
      // (pas authStore.logout, qui appellerait POST /api/auth/logout sur une session qui n'a
      // plus de compte à révoquer) purge le jeton en mémoire et laisse ProtectedRoute rediriger.
      notifySessionExpired()
    } catch (err) {
      setStatus('error')
      setError(err instanceof ApiError ? err.message : 'Impossible de supprimer le compte.')
    }
  }

  return (
    <section aria-labelledby="delete-heading" className="rounded-card border border-red bg-white p-5 shadow-card">
      <h2 id="delete-heading" className="mb-1 text-base font-semibold text-text">
        Suppression du compte
      </h2>
      <p className="mb-1 text-sm text-text">
        Cette action est irréversible. Elle supprime immédiatement l&apos;intégralité de votre entreprise : tous ses
        comptes utilisateurs — pas seulement le vôtre — et tous ses diagnostics, réponses, scores et rapports. Rien de
        tout cela n&apos;est conservé.
      </p>
      <p className="mb-3 text-sm text-text-muted">
        Cette portée s&apos;applique quel que soit votre rôle, y compris si d&apos;autres comptes existent dans votre
        entreprise.
      </p>
      <form onSubmit={(e) => void handleSubmit(e)} noValidate className="flex flex-col gap-3">
        <div>
          <label htmlFor="delete-confirmation-email" className="mb-1 block text-sm text-text">
            Pour confirmer, saisissez votre adresse e-mail ({email})
          </label>
          <input
            id="delete-confirmation-email"
            type="email"
            required
            value={confirmationEmail}
            onChange={(e) => setConfirmationEmail(e.target.value)}
            className="w-full max-w-sm rounded-button border border-border px-3 py-2 text-text"
          />
        </div>
        <div>
          <label htmlFor="delete-password" className="mb-1 block text-sm text-text">
            Mot de passe
          </label>
          <input
            id="delete-password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="w-full max-w-sm rounded-button border border-border px-3 py-2 text-text"
          />
        </div>
        {status === 'error' && error && (
          <p role="alert" className="text-sm text-red">
            {error}
          </p>
        )}
        <button
          type="submit"
          disabled={!emailMatches || status === 'submitting'}
          className="w-fit rounded-button bg-red px-4 py-2 font-medium text-white shadow-button disabled:cursor-not-allowed disabled:opacity-50"
        >
          {status === 'submitting' ? 'Suppression…' : 'Supprimer définitivement mon compte et mon entreprise'}
        </button>
      </form>
    </section>
  )
}
