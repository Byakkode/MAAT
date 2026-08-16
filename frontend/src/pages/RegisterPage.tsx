import { type FormEvent, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import type { CompanySizeRange } from '../types/auth'

const SIZE_RANGE_LABELS: Record<CompanySizeRange, string> = {
  Micro: 'Micro-entreprise (moins de 10 salariés)',
  Small: 'Petite entreprise (10 à 49 salariés)',
  Medium: 'Moyenne entreprise (50 à 249 salariés)',
  Large: 'Grande entreprise (250 salariés ou plus)',
}

export function RegisterPage() {
  const register = useAuthStore((state) => state.register)
  const error = useAuthStore((state) => state.error)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [companyName, setCompanyName] = useState('')
  const [sectorCode, setSectorCode] = useState('')
  const [sizeRange, setSizeRange] = useState<CompanySizeRange>('Micro')
  const [region, setRegion] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)
    setSuccessMessage(null)
    try {
      const result = await register({ email, password, companyName, sectorCode, sizeRange, region })
      setSuccessMessage(result.message)
    } catch {
      // L'erreur est déjà exposée par le store (state.error), affichée ci-dessous.
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className="mx-auto max-w-sm rounded-card border border-border bg-white p-5 shadow-card">
      <h1 className="mb-4 text-2xl font-semibold text-text">Inscription</h1>
      <form onSubmit={handleSubmit} noValidate>
        <div className="mb-4">
          <label htmlFor="register-email" className="mb-1 block text-sm text-text">
            Adresse e-mail
          </label>
          <input
            id="register-email"
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="w-full rounded-button border border-border px-3 py-2 text-text"
          />
        </div>
        <div className="mb-4">
          <label htmlFor="register-password" className="mb-1 block text-sm text-text">
            Mot de passe
          </label>
          <input
            id="register-password"
            type="password"
            autoComplete="new-password"
            minLength={12}
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="w-full rounded-button border border-border px-3 py-2 text-text"
          />
        </div>
        <div className="mb-4">
          <label htmlFor="register-company-name" className="mb-1 block text-sm text-text">
            Nom de l'entreprise
          </label>
          <input
            id="register-company-name"
            type="text"
            required
            value={companyName}
            onChange={(e) => setCompanyName(e.target.value)}
            className="w-full rounded-button border border-border px-3 py-2 text-text"
          />
        </div>
        <div className="mb-4">
          <label htmlFor="register-sector-code" className="mb-1 block text-sm text-text">
            Code NAF
          </label>
          <input
            id="register-sector-code"
            type="text"
            required
            value={sectorCode}
            onChange={(e) => setSectorCode(e.target.value)}
            className="w-full rounded-button border border-border px-3 py-2 text-text"
          />
        </div>
        <div className="mb-4">
          <label htmlFor="register-size-range" className="mb-1 block text-sm text-text">
            Tranche d'effectif
          </label>
          <select
            id="register-size-range"
            value={sizeRange}
            onChange={(e) => setSizeRange(e.target.value as CompanySizeRange)}
            className="w-full rounded-button border border-border px-3 py-2 text-text"
          >
            {Object.entries(SIZE_RANGE_LABELS).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </div>
        <div className="mb-4">
          <label htmlFor="register-region" className="mb-1 block text-sm text-text">
            Région
          </label>
          <input
            id="register-region"
            type="text"
            required
            value={region}
            onChange={(e) => setRegion(e.target.value)}
            className="w-full rounded-button border border-border px-3 py-2 text-text"
          />
        </div>
        {error && (
          <p role="alert" className="mb-4 text-sm text-red">
            {error}
          </p>
        )}
        {successMessage && (
          <p role="status" className="mb-4 text-sm text-green-maat-text">
            {successMessage}
          </p>
        )}
        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded-button bg-blue-maat px-4 py-2 font-medium text-white shadow-button disabled:opacity-60"
        >
          S'inscrire
        </button>
      </form>
      <p className="mt-4 text-sm text-text-muted">
        Déjà un compte ? <Link to="/login" className="text-blue-maat-text">Se connecter</Link>
      </p>
    </section>
  )
}
