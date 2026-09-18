import { LogOut, Menu } from 'lucide-react'
import { Link } from 'react-router-dom'
import { ROLE_LABELS } from '../../constants/roleLabels'
import { useAuthStore } from '../../store/authStore'
import { useCurrentUserStore } from '../../store/currentUserStore'

interface HeaderProps {
  onOpenMobileNav: () => void
}

export function Header({ onOpenMobileNav }: HeaderProps) {
  const role = useAuthStore((s) => s.user?.role)
  const logout = useAuthStore((s) => s.logout)
  const email = useCurrentUserStore((s) => s.email)
  const companyName = useCurrentUserStore((s) => s.companyName)

  const roleLabel = role ? (ROLE_LABELS[role] ?? role) : null
  const initial = ((companyName ?? email ?? '?')[0] ?? '?').toUpperCase()

  return (
    <header className="flex items-center justify-between gap-4 border-b border-border bg-white px-4 py-2.5">
      {/* Gauche : hamburger mobile + nom entreprise */}
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={onOpenMobileNav}
          aria-label="Ouvrir la navigation"
          className="rounded-button p-1.5 text-text-muted hover:bg-bg md:hidden"
        >
          <Menu size={20} strokeWidth={1.5} aria-hidden="true" />
        </button>
        <span className="font-heading font-semibold text-text">{companyName}</span>
      </div>

      {/* Droite : avatar + infos utilisateur + actions */}
      <div className="flex items-center gap-3 text-sm">
        {/* Avatar initiale entreprise */}
        <div
          aria-hidden
          className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-blue-maat text-xs font-bold text-white"
        >
          {initial}
        </div>

        {/* Email + rôle empilés */}
        <div className="hidden flex-col items-end leading-tight sm:flex">
          {email && <span className="text-xs text-text-muted">{email}</span>}
          {roleLabel && <span className="text-xs font-medium text-text">{roleLabel}</span>}
        </div>

        {/* Séparateur */}
        <span aria-hidden className="h-5 w-px bg-border" />

        <Link to="/compte" className="font-medium text-blue-maat-text hover:underline">
          Mon compte
        </Link>

        <button
          type="button"
          onClick={() => void logout()}
          className="flex items-center gap-1.5 font-medium text-text-muted transition-colors hover:text-text"
        >
          <LogOut size={16} strokeWidth={1.5} aria-hidden="true" />
          Déconnexion
        </button>
      </div>
    </header>
  )
}
