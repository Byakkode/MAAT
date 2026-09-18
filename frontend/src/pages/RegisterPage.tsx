import { type FormEvent, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import type { CompanySizeRange } from '../types/auth'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'

const AUTH_BG: React.CSSProperties = {
  background:
    'radial-gradient(ellipse at 25% 30%, rgba(21,101,255,0.65) 0%, transparent 55%),' +
    'radial-gradient(ellipse at 80% 75%, rgba(13,40,110,0.85) 0%, transparent 55%),' +
    '#0d1b3e',
}

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
    <div className="flex min-h-screen items-center justify-center p-4 py-10" style={AUTH_BG}>
      <div className="w-full max-w-md rounded-2xl bg-white px-8 py-10 shadow-2xl">
        {/* Logo */}
        <div className="mx-auto mb-6 flex h-12 w-12 items-center justify-center rounded-full bg-blue-maat">
          <span className="text-sm font-bold text-white">M</span>
        </div>

        <h1 className="mb-1 text-center text-2xl font-semibold text-text">Inscription</h1>
        <p className="mb-8 text-center text-sm text-text-muted">
          Créez votre espace RSE en quelques minutes.
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

          <div className="grid grid-cols-2 gap-4">
            <Input
              label="Code NAF"
              id="register-sector-code"
              name="sectorCode"
              type="text"
              required
              value={sectorCode}
              onChange={(e) => setSectorCode(e.target.value)}
            />

            <div className="flex flex-col gap-1">
              <label htmlFor="register-size-range" className="text-sm font-medium text-text">
                Tranche d&apos;effectif
              </label>
              <select
                id="register-size-range"
                value={sizeRange}
                onChange={(e) => setSizeRange(e.target.value as CompanySizeRange)}
                className="w-full rounded-button border border-border px-3 py-2 text-sm text-text focus:border-blue-maat focus:outline-none focus:ring-2 focus:ring-blue-maat/20"
              >
                {Object.entries(SIZE_RANGE_LABELS).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <Input
            label="Région"
            id="register-region"
            name="region"
            type="text"
            required
            value={region}
            onChange={(e) => setRegion(e.target.value)}
          />

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

        <p className="mt-6 text-center text-sm text-text-muted">
          Déjà un compte ?{' '}
          <Link to="/login" className="font-medium text-blue-maat-text hover:underline">
            Se connecter
          </Link>
        </p>
      </div>
    </div>
  )
}
