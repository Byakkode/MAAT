import { type FormEvent, useState } from 'react'
import * as accountApi from '../../api/accountApi'
import { ApiError } from '../../api/authApi'

type Status = 'idle' | 'submitting' | 'success' | 'error'

// docs/specs/coquille-et-compte.md, section 5 : droit à la portabilité, déjà implémenté côté
// serveur (POST /api/me/export) — cette carte ne fait que le rendre atteignable.
export function DataExportCard() {
  const [password, setPassword] = useState('')
  const [status, setStatus] = useState<Status>('idle')
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setStatus('submitting')
    setError(null)
    try {
      await accountApi.exportData(password)
      setStatus('success')
      setPassword('')
    } catch (err) {
      setStatus('error')
      setError(err instanceof ApiError ? err.message : "Impossible d'exporter vos données.")
    }
  }

  return (
    <section aria-labelledby="export-heading" className="rounded-card border border-border bg-white p-5 shadow-card">
      <h2 id="export-heading" className="mb-1 text-base font-semibold text-text">
        Mes données
      </h2>
      <p className="mb-3 text-sm text-text-muted">
        Exportez l&apos;ensemble de vos données personnelles et de vos diagnostics, dans un fichier JSON réexploitable.
      </p>
      <form onSubmit={(e) => void handleSubmit(e)} noValidate className="flex flex-col gap-3">
        <div>
          <label htmlFor="export-password" className="mb-1 block text-sm text-text">
            Mot de passe
          </label>
          <input
            id="export-password"
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
        {status === 'success' && (
          <p role="status" className="text-sm text-green-maat-text">
            Export téléchargé.
          </p>
        )}
        <button
          type="submit"
          disabled={status === 'submitting'}
          className="w-fit rounded-button border border-blue-maat px-4 py-2 font-medium text-blue-maat shadow-button disabled:opacity-60"
        >
          {status === 'submitting' ? 'Export…' : 'Exporter mes données'}
        </button>
      </form>
    </section>
  )
}
