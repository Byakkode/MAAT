import { type FormEvent, useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { BarChart2, CheckCircle, FileText, Plus, X } from 'lucide-react'
import { useAuthStore } from '../store/authStore'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { LogoHorizontal } from '../components/ui/Logo'
import { AuthPanel } from '../components/auth/AuthPanel'
import { SectionLabel } from '../landing/primitives'
import type { AuthPanelItem } from '../components/auth/AuthPanel'

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

// ── Panneau de gauche ────────────────────────────────────────────────────────

// L'utilisateur qui se connecte a déjà un espace : on lui rappelle ce qu'il y retrouve, pas
// le parcours d'inscription. Icônes identiques à celles de la barre latérale (Sidebar.tsx).
const RETURN_TITLE = (
  <>
    Bon retour. <span className="text-white/45">Votre démarche RSE vous attend.</span>
  </>
)

const RETURN_ITEMS: AuthPanelItem[] = [
  { icon: BarChart2, title: 'Votre tableau de bord', text: 'Votre score et son évolution d’un diagnostic à l’autre.' },
  { icon: CheckCircle, title: 'Votre plan d’actions', text: 'Les actions en cours, leurs responsables et leurs échéances.' },
  { icon: FileText, title: 'Vos rapports', text: 'Le rapport selon la norme volontaire (ex-VSME), toujours à jour de vos dernières réponses.' },
]

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
      // loginIntro : AppShell joue l'animation du logo à l'arrivée (LoginIntro.tsx).
      navigate('/tableau-de-bord', { state: { loginIntro: true } })
    } catch {
      // L'erreur est déjà exposée par le store (state.error), affichée ci-dessous.
    } finally {
      setSubmitting(false)
    }
  }

  // Mise en page alignée sur l'inscription et la page d'accueil : panneau éditorial sombre à
  // gauche (AuthPanel), formulaire à plat sur fond blanc.
  return (
    <div className="landing flex min-h-screen bg-white">
      <AuthPanel title={RETURN_TITLE} items={RETURN_ITEMS} />
      <div className="flex flex-1 flex-col px-4 py-6 sm:px-10 lg:px-16">
        <div className="flex items-center justify-between gap-4">
          {/* Logo : seulement quand le panneau de gauche (qui le porte) est masqué. */}
          <Link to="/" aria-label="MAAT, retour à l’accueil" className="lg:hidden">
            <LogoHorizontal />
          </Link>
          <p className="ml-auto whitespace-nowrap text-[14px] text-text-muted">
            {/* Question masquée sur téléphone : elle heurterait le logo, le lien suffit. */}
            <span className="max-sm:hidden">Pas encore de compte ? </span>
            <Link to="/register" className="font-heading font-medium text-text underline decoration-border-strong underline-offset-4 hover:decoration-blue-maat">
              Créer un compte
            </Link>
          </p>
        </div>

        <div className="mx-auto flex w-full max-w-[480px] flex-1 flex-col justify-center py-12">
          <h1 className="display text-[clamp(2.25rem,4vw,3rem)] text-text">Connexion</h1>
          <p className="mt-3 mb-10 text-[16px] leading-relaxed text-text-muted">
            Accédez à votre tableau de bord RSE.
          </p>

          {/* Connexions récentes : liste à filets, comme le choix de secteur de la page
              d'accueil. Absente tant qu'aucun compte n'a été mémorisé sur ce navigateur. */}
          {recentEmails.length > 0 && (
            <section aria-labelledby="recent-title" className="mb-10">
              <div className="flex items-baseline justify-between gap-4">
                <SectionLabel index="01">
                  <span id="recent-title">Connexions récentes</span>
                </SectionLabel>
                <button
                  type="button"
                  onClick={handleAddAccount}
                  className="inline-flex cursor-pointer items-center gap-1.5 text-[13px] font-medium text-text-muted transition-colors hover:text-text"
                >
                  <Plus size={14} aria-hidden="true" />
                  Autre compte
                </button>
              </div>
              <ul className="mt-4 border-t border-border">
                {recentEmails.map((recent) => {
                  const selected = email === recent
                  return (
                    <li key={recent} className="flex items-center border-b border-border">
                      <button
                        type="button"
                        onClick={() => handleSelectAccount(recent)}
                        aria-pressed={selected}
                        className={`group flex min-w-0 flex-1 cursor-pointer items-center gap-4 py-3 text-left transition-colors ${selected ? 'text-text' : 'text-text-muted hover:text-text'}`}
                      >
                        <span
                          className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-md font-heading text-[13px] font-semibold transition-colors ${selected ? 'bg-blue-maat text-white' : 'bg-bg text-text-muted group-hover:text-text'}`}
                          aria-hidden="true"
                        >
                          {emailInitial(recent)}
                        </span>
                        <span className={`truncate text-[15px] ${selected ? 'font-medium' : ''}`}>{recent}</span>
                      </button>
                      <button
                        type="button"
                        onClick={() => handleRemoveAccount(recent)}
                        aria-label={`Retirer ${recent} des connexions récentes`}
                        className="flex h-8 w-8 shrink-0 cursor-pointer items-center justify-center rounded-md text-text-muted transition-colors hover:bg-bg hover:text-text"
                      >
                        <X size={15} aria-hidden="true" />
                      </button>
                    </li>
                  )
                })}
              </ul>
            </section>
          )}

          <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
            {recentEmails.length > 0 && <SectionLabel index="02">Identifiants</SectionLabel>}
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

            <Button type="submit" size="lg" isLoading={submitting} className="mt-3 w-full py-3 font-heading text-[15px]">
              Se connecter
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}
