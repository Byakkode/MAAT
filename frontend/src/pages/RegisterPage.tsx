import { type FormEvent, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import type { CompanySizeRange } from '../types/auth'
import { AuthPanel } from '../components/auth/AuthPanel'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { NafCombobox } from '../components/ui/NafCombobox'
import { REGIONS, type Region } from '../constants/regions'
import { LogoHorizontal } from '../components/ui/Logo'
import { SectionLabel } from '../landing/primitives'

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

  // Mise en page alignée sur la page d'accueil (src/landing/) : panneau éditorial sombre à
  // gauche (AuthPanel), formulaire à plat sur fond blanc, sections numérotées.
  return (
    <div className="landing flex min-h-screen bg-white">
      <AuthPanel />
      <div className="flex flex-1 flex-col px-4 py-4 sm:px-10 lg:px-16">
        <div className="flex items-center justify-between gap-4">
          {/* Logo : seulement quand le panneau de gauche (qui le porte) est masqué. */}
          <Link to="/" aria-label="MAAT, retour à l’accueil" className="lg:hidden">
            <LogoHorizontal />
          </Link>
          <p className="ml-auto whitespace-nowrap text-[14px] text-text-muted">
            {/* Question masquée sur téléphone : elle heurterait le logo, le lien suffit. */}
            <span className="max-sm:hidden">Déjà un compte ? </span>
            <Link to="/login" className="font-heading font-medium text-text underline decoration-border-strong underline-offset-4 hover:decoration-blue-maat">
              Se connecter
            </Link>
          </p>
        </div>

        {/* Tient sans défilement sur un écran d'ordinateur portable (1366×768) : champs
            courts groupés par deux à partir de sm, champs aux valeurs longues (secteur NAF,
            tranche d'effectif) sur toute la largeur. Sur téléphone, tout passe en une colonne
            et la page défile, ce qui est attendu à cette taille. */}
        <div className="mx-auto flex w-full max-w-[560px] flex-1 flex-col justify-center py-3 max-lg:pt-10">
          <h1 className="display text-[clamp(2rem,3.4vw,2.625rem)] text-text">Créer un compte</h1>
          <p className="mt-2 mb-6 text-[15px] leading-relaxed text-text-muted">
            Votre espace RSE en quelques minutes. Le diagnostic commence juste après.
          </p>

          <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
            <SectionLabel index="01">Identifiants</SectionLabel>

            <div className="grid gap-4 sm:grid-cols-2">
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
            </div>

            <div className="mt-1">
              <SectionLabel index="02">Votre entreprise</SectionLabel>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <Input
                label="Nom de l'entreprise"
                id="register-company-name"
                name="companyName"
                type="text"
                required
                value={companyName}
                onChange={(e) => setCompanyName(e.target.value)}
              />
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

            <NafCombobox
              label="Secteur d'activité (code NAF)"
              value={sectorCode}
              onChange={setSectorCode}
              required
            />

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

            <Button type="submit" size="lg" isLoading={submitting} className="mt-2 w-full py-3 font-heading text-[15px]">
              S&apos;inscrire
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}
