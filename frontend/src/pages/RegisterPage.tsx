import { type FormEvent, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import type { CompanySizeRange } from '../types/auth'
import { AuthPanel } from '../components/auth/AuthPanel'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { NafCombobox } from '../components/ui/NafCombobox'
import { REGIONS, type Region } from '../constants/regions'

const AUTH_BG: React.CSSProperties = { background: '#0c1322' }

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
  const [region, setRegion] = useState<Region>(REGIONS[0])
  const [submitting, setSubmitting] = useState(false)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    // Même précaution que LoginPage : lire les valeurs DOM réelles pour gérer l'auto-remplissage.
    const fd = new FormData(event.currentTarget)
    const payload = {
      email: (fd.get('email') as string | null) || email,
      password: (fd.get('password') as string | null) || password,
      companyName: (fd.get('companyName') as string | null) || companyName,
      sectorCode: (fd.get('sectorCode') as string | null) || sectorCode,
      sizeRange,
      region: (fd.get('region') as string | null) || region,
    }
    setSubmitting(true)
    setSuccessMessage(null)
    try {
      const result = await register(payload)
      setSuccessMessage(result.message)
    } catch {
      // L'erreur est déjà exposée par le store (state.error), affichée ci-dessous.
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-screen" style={AUTH_BG}>
      <AuthPanel />
      <div className="flex flex-1 items-center justify-center p-6 py-10">
      <div className="w-full max-w-md">
        {/* Logo mobile (masqué quand AuthPanel visible) */}
        <div className="mb-6 lg:hidden">
          <div className="flex items-center gap-3">
            <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-blue-maat">
              <span className="text-[13px] font-bold text-white">M</span>
            </div>
            <p className="text-[15px] font-bold leading-tight tracking-tight text-white">MAAT</p>
          </div>
        </div>

        <div className="rounded-2xl bg-white px-8 py-9 shadow-[0_20px_60px_rgba(0,0,0,0.35)]">
        <h1 className="mb-1 text-[1.375rem] font-semibold text-text">Créer un compte</h1>
        <p className="mb-7 text-[13px] text-text-muted">
          Votre espace RSE en quelques minutes.
        </p>

        <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
          {/* Séparateur visuel — Identifiants */}
          <p className="text-xs font-semibold uppercase tracking-wide text-text-muted">
            Identifiants
          </p>

          <Input
            label="Adresse e-mail"
            id="register-email"
            name="email"
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
          <Input
            label="Mot de passe"
            id="register-password"
            name="password"
            type="password"
            autoComplete="new-password"
            minLength={12}
            required
            hint="12 caractères minimum"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />

          {/* Séparateur visuel — Entreprise */}
          <p className="mt-2 text-xs font-semibold uppercase tracking-wide text-text-muted">
            Votre entreprise
          </p>

          <Input
            label="Nom de l'entreprise"
            id="register-company-name"
            name="companyName"
            type="text"
            required
            value={companyName}
            onChange={(e) => setCompanyName(e.target.value)}
          />

          <NafCombobox
            label="Secteur d'activité (code NAF)"
            value={sectorCode}
            onChange={setSectorCode}
            required
          />

          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="register-size-range" className="text-[13px] font-medium text-text">
                Tranche d&apos;effectif
              </label>
              <select
                id="register-size-range"
                value={sizeRange}
                onChange={(e) => setSizeRange(e.target.value as CompanySizeRange)}
                className="w-full rounded-[10px] border border-border bg-white px-3.5 py-2.5 text-sm text-text shadow-[0_1px_2px_rgba(0,0,0,0.04)] focus:border-blue-maat/70 focus:outline-none focus:ring-2 focus:ring-blue-maat/10 transition-all duration-150"
              >
                {Object.entries(SIZE_RANGE_LABELS).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </div>

            <div className="flex flex-col gap-1.5">
              <label htmlFor="register-region" className="text-[13px] font-medium text-text">
                Région
              </label>
              <select
                id="register-region"
                value={region}
                onChange={(e) => setRegion(e.target.value as Region)}
                className="w-full rounded-[10px] border border-border bg-white px-3.5 py-2.5 text-sm text-text shadow-[0_1px_2px_rgba(0,0,0,0.04)] focus:border-blue-maat/70 focus:outline-none focus:ring-2 focus:ring-blue-maat/10 transition-all duration-150"
              >
                {REGIONS.map((r) => (
                  <option key={r} value={r}>
                    {r}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {error && (
            <p role="alert" className="text-sm text-red">
              {error}
            </p>
          )}
          {successMessage && (
            <p
              role="status"
              className="rounded-button border border-green-maat/30 bg-green-maat/10 px-3 py-2 text-sm text-green-maat-text"
            >
              {successMessage}
            </p>
          )}

          <Button type="submit" isLoading={submitting} className="w-full mt-1">
            S&apos;inscrire
          </Button>
        </form>

        <p className="mt-6 text-center text-[13px] text-text-muted">
          Déjà un compte ?{' '}
          <Link to="/login" className="font-medium text-blue-maat-text hover:underline">
            Se connecter
          </Link>
        </p>
        </div>
      </div>
      </div>
    </div>
  )
}
