import { KeyRound } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import * as accountApi from '../../api/accountApi'
import { ApiError } from '../../api/authApi'
import { Card } from '../ui/Card'

type Status = 'idle' | 'submitting' | 'success' | 'error'

const inputClass =
  'w-full max-w-sm rounded-xl border border-border bg-white px-3 py-2.5 text-[13.5px] text-text transition-colors focus:border-blue-maat focus:outline-none focus:ring-2 focus:ring-blue-maat/20'

// docs/specs/coquille-et-compte.md, section 5 : "Un changement réussi invalide les autres
// sessions — comportement attendu, à annoncer avant validation et non après." L'avertissement
// est donc posé au-dessus du formulaire, pas dans le message de succès.
export function PasswordChangeCard() {
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [status, setStatus] = useState<Status>('idle')
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setStatus('submitting')
    setError(null)
    try {
      await accountApi.changePassword(currentPassword, newPassword)
      setStatus('success')
      setCurrentPassword('')
      setNewPassword('')
    } catch (err) {
      setStatus('error')
      setError(err instanceof ApiError ? err.message : 'Impossible de modifier le mot de passe.')
    }
  }

  return (
    <Card as="section" aria-labelledby="password-heading">
      <h2 id="password-heading" className="mb-1 flex items-center gap-2 text-base font-semibold text-text">
        <KeyRound size={15} className="shrink-0 text-text-muted" aria-hidden="true" />
        Mot de passe
      </h2>
      <p className="mb-4 text-[13px] text-text-muted">
        Un changement réussi déconnecte toutes vos autres sessions ; celle-ci reste connectée.
      </p>
      <form onSubmit={(e) => void handleSubmit(e)} noValidate className="flex flex-col gap-3">
        <div>
          <label htmlFor="current-password" className="mb-1.5 block text-[13px] font-medium text-text">
            Mot de passe actuel
          </label>
          <input
            id="current-password"
            type="password"
            autoComplete="current-password"
            required
            value={currentPassword}
            onChange={(e) => setCurrentPassword(e.target.value)}
            className={inputClass}
          />
        </div>
        <div>
          <label htmlFor="new-password" className="mb-1.5 block text-[13px] font-medium text-text">
            Nouveau mot de passe
          </label>
          <input
            id="new-password"
            type="password"
            autoComplete="new-password"
            required
            minLength={12}
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            className={inputClass}
          />
        </div>
        {status === 'error' && error && (
          <p role="alert" className="text-[13px] text-red">
            {error}
          </p>
        )}
        {status === 'success' && (
          <p role="status" className="text-[13px] text-green-maat-text">
            Mot de passe modifié.
          </p>
        )}
        <button
          type="submit"
          disabled={status === 'submitting'}
          className="w-fit rounded-xl bg-blue-maat px-5 py-2.5 text-[13.5px] font-semibold text-white shadow-button-primary transition-opacity disabled:opacity-60"
        >
          {status === 'submitting' ? 'Modification…' : 'Modifier le mot de passe'}
        </button>
      </form>
    </Card>
  )
}
