import { type FormEvent, useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Plus, X } from 'lucide-react'
import { useAuthStore } from '../store/authStore'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'

// ── Cache localStorage ───────────────────────────────────────────────────────

const RECENT_KEY = 'maat_recent_emails'
const LEGACY_KEY = 'maat_last_email'
const MAX_RECENT = 3

function loadRecentEmails(): string[] {
  try {
    const raw = localStorage.getItem(RECENT_KEY)
    if (raw) {
      const parsed: unknown = JSON.parse(raw)
      if (Array.isArray(parsed)) return (parsed as unknown[]).filter((s): s is string => typeof s === 'string').slice(0, MAX_RECENT)
    }
    // Migration depuis l'ancien format (clé unique)
    const legacy = localStorage.getItem(LEGACY_KEY)
    if (legacy) return [legacy]
    return []
  } catch {
    return []
  }
}

function saveRecentEmail(email: string): string[] {
  const updated = [email, ...loadRecentEmails().filter((e) => e !== email)].slice(0, MAX_RECENT)
  try {
    localStorage.setItem(RECENT_KEY, JSON.stringify(updated))
    localStorage.removeItem(LEGACY_KEY)
  } catch { /* ignore */ }
  return updated
}

function deleteRecentEmail(email: string): string[] {
  const updated = loadRecentEmails().filter((e) => e !== email)
  try { localStorage.setItem(RECENT_KEY, JSON.stringify(updated)) } catch { /* ignore */ }
  return updated
}

// ── Helpers visuels ──────────────────────────────────────────────────────────

function emailInitial(email: string): string {
  return (email.split('@')[0]?.[0] ?? '?').toUpperCase()
}

const AUTH_BG = { background: '#0c1322' } as const

// ── Page ─────────────────────────────────────────────────────────────────────

export function LoginPage() {
  const login = useAuthStore((state) => state.login)
  const error = useAuthStore((state) => state.error)
  const navigate = useNavigate()

  const [recentEmails, setRecentEmails] = useState<string[]>([])
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const emailRef = useRef<HTMLInputElement>(null)
  const passwordRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    setRecentEmails(loadRecentEmails())
  }, [])

  function handleSelectAccount(selected: string) {
    setEmail(selected)
    setTimeout(() => passwordRef.current?.focus(), 0)
  }

  function handleRemoveAccount(toRemove: string) {
    setRecentEmails(deleteRecentEmail(toRemove))
    if (email === toRemove) setEmail('')
  }

  function handleAddAccount() {
    setEmail('')
    setTimeout(() => emailRef.current?.focus(), 0)
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    // Lire les valeurs DOM réelles via FormData : l'auto-remplissage du navigateur remplit
    // les champs visuellement sans déclencher onChange, laissant le state React vide.
    const fd = new FormData(event.currentTarget)
    const loginEmail = (fd.get('email') as string | null) || email
    const loginPassword = (fd.get('password') as string | null) || password
    setSubmitting(true)
    try {
      await login(loginEmail, loginPassword)
      setRecentEmails(saveRecentEmail(loginEmail))
      navigate('/')
    } catch {
      // L'erreur est déjà exposée par le store (state.error), affichée ci-dessous.
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-4" style={AUTH_BG}>
      <div className="flex w-full max-w-2xl overflow-hidden rounded-2xl bg-white shadow-[0_20px_60px_rgba(0,0,0,0.35)]">

        {/* ── Panneau gauche : connexions récentes ──────────────────────── */}
        <aside className="hidden w-60 shrink-0 flex-col border-r border-border bg-bg p-8 md:flex">
          {/* Logo */}
          <div className="mb-8 flex h-8 w-8 items-center justify-center rounded-lg bg-blue-maat">
            <span className="text-[13px] font-bold text-white">M</span>
          </div>

          <h2 className="mb-0.5 text-[13px] font-semibold text-text">Connexions récentes</h2>
          <p className="mb-6 text-[12px] text-text-muted">
            Cliquez sur votre compte ou ajoutez-en un autre.
          </p>

          <div className="grid grid-cols-2 gap-3">
            {recentEmails.map((recent) => (
              <div key={recent} className="relative">
                <button
                  type="button"
                  onClick={() => handleSelectAccount(recent)}
                  className={[
                    'flex w-full flex-col items-center gap-2 rounded-xl border px-2 py-3 text-center',
                    'transition-colors hover:bg-blue-maat/[0.04]',
                    'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-maat',
                    email === recent ? 'border-blue-maat/40 bg-blue-maat/[0.04]' : 'border-border bg-white',
                  ].join(' ')}
                >
                  <span className="flex h-9 w-9 items-center justify-center rounded-full bg-blue-maat text-[13px] font-bold text-white">
                    {emailInitial(recent)}
                  </span>
                  <span className="w-full truncate text-[11px] text-text-muted" title={recent}>
                    {recent.split('@')[0]}
                  </span>
                </button>

                {/* Supprimer du cache */}
                <button
                  type="button"
                  onClick={() => handleRemoveAccount(recent)}
                  aria-label={`Retirer ${recent} des connexions récentes`}
                  className="absolute -right-1.5 -top-1.5 flex h-5 w-5 items-center justify-center rounded-full bg-text-muted text-white transition-colors hover:bg-text focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-blue-maat"
                >
                  <X size={10} aria-hidden />
                </button>
              </div>
            ))}

            {/* Tile "Autre compte" */}
            <button
              type="button"
              onClick={handleAddAccount}
              className={[
                'flex flex-col items-center gap-2 rounded-xl border border-dashed px-2 py-3 text-center',
                'transition-colors hover:bg-blue-maat/[0.04]',
                'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-maat',
                recentEmails.length === 0 ? 'border-blue-maat/30' : 'border-border',
              ].join(' ')}
            >
              <span className="flex h-9 w-9 items-center justify-center rounded-full border-2 border-dashed border-border text-text-muted">
                <Plus size={15} aria-hidden />
              </span>
              <span className="text-[11px] text-text-muted">
                {recentEmails.length === 0 ? 'Connexion' : 'Autre compte'}
              </span>
            </button>
          </div>
        </aside>

        {/* ── Panneau droit : formulaire ────────────────────────────────── */}
        <div className="flex flex-1 flex-col justify-center px-8 py-10">
          {/* Logo mobile uniquement */}
          <div className="mb-6 flex h-8 w-8 items-center justify-center rounded-lg bg-blue-maat md:hidden">
            <span className="text-[13px] font-bold text-white">M</span>
          </div>

          <h1 className="mb-1 text-[1.375rem] font-semibold text-text">Connexion</h1>
          <p className="mb-7 text-[13px] text-text-muted">Accédez à votre tableau de bord RSE.</p>

          <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
            <Input
              ref={emailRef}
              label="Adresse e-mail"
              id="login-email"
              name="email"
              type="email"
              autoComplete="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
            <Input
              ref={passwordRef}
              label="Mot de passe"
              id="login-password"
              name="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />

            {error && (
              <p role="alert" className="text-sm text-red">
                {error}
              </p>
            )}

            <Button type="submit" isLoading={submitting} className="w-full mt-1">
              Se connecter
            </Button>
          </form>

          <p className="mt-6 text-sm text-text-muted">
            Pas encore de compte ?{' '}
            <Link to="/register" className="font-medium text-blue-maat-text hover:underline">
              Créer un compte
            </Link>
          </p>
        </div>
      </div>
    </div>
  )
}
