import { Download } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import * as accountApi from '../../api/accountApi'
import { ApiError } from '../../api/authApi'
import { Card } from '../ui/Card'

type Status = 'idle' | 'submitting' | 'success' | 'error'

const inputClass =
  'w-full max-w-sm rounded-xl border border-border bg-white px-3 py-2.5 text-[13.5px] text-text transition-colors focus:border-blue-maat focus:outline-none focus:ring-2 focus:ring-blue-maat/20'

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
    <Card as="section" aria-labelledby="export-heading">
      <h2 id="export-heading" className="mb-1 flex items-center gap-2 text-base font-semibold text-text">
        <Download size={15} className="shrink-0 text-text-muted" aria-hidden="true" />
        Mes données
      </h2>
      <p className="mb-4 text-[13px] text-text-muted">
        Exportez l&apos;ensemble de vos données personnelles et de vos diagnostics, dans un fichier JSON réexploitable.
      </p>
      <form onSubmit={(e) => void handleSubmit(e)} noValidate className="flex flex-col gap-3">
        <div>
          <label htmlFor="export-password" className="mb-1.5 block text-[13px] font-medium text-text">
            Mot de passe
          </label>
          <input
            id="export-password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
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
            Export téléchargé.
          </p>
        )}
        <button
          type="submit"
          disabled={status === 'submitting'}
          className="w-fit rounded-xl border border-blue-maat px-5 py-2.5 text-[13.5px] font-medium text-blue-maat transition-colors hover:bg-blue-maat/5 disabled:opacity-60"
        >
          {status === 'submitting' ? 'Export…' : 'Exporter mes données'}
        </button>
      </form>
    </Card>
  )
}
