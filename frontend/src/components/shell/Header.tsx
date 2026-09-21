import { LogOut, Menu, Settings } from 'lucide-react'
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
    <header className="sticky top-0 z-10 flex items-center justify-between gap-4 border-b border-border bg-white/96 px-5 py-3 shadow-[0_1px_0_rgba(0,0,0,0.05)] backdrop-blur-md">
      {/* Gauche : hamburger mobile + nom entreprise */}
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={onOpenMobileNav}
          aria-label="Ouvrir la navigation"
          className="rounded-lg p-1.5 text-text-muted transition-colors hover:bg-bg hover:text-text md:hidden"
        >
          <Menu size={18} strokeWidth={1.5} aria-hidden="true" />
        </button>
        <span className="font-heading text-[15px] font-semibold text-text">{companyName}</span>
      </div>

      {/* Droite : actions + identité */}
      <div className="flex items-center gap-1">
        <Link
          to="/compte"
          className="flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-[13px] font-medium text-text-muted transition-colors hover:bg-bg hover:text-text"
        >
          <Settings size={14} strokeWidth={1.5} aria-hidden="true" />
          <span className="hidden sm:block">Mon compte</span>
        </Link>

        <button
          type="button"
          onClick={() => void logout()}
          className="flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-[13px] font-medium text-text-muted transition-colors hover:bg-bg hover:text-text"
        >
          <LogOut size={14} strokeWidth={1.5} aria-hidden="true" />
          <span className="hidden sm:block">Déconnexion</span>
        </button>

        {/* Séparateur */}
        <span aria-hidden className="mx-1 h-4 w-px bg-border" />

        {/* Avatar + identité */}
        <div className="flex items-center gap-2.5">
          <div className="hidden flex-col items-end leading-tight sm:flex">
            {roleLabel && <span className="text-[12px] font-semibold text-text">{roleLabel}</span>}
            {email && <span className="max-w-[150px] truncate text-[11px] text-text-muted">{email}</span>}
          </div>
          <div
            aria-hidden
            className="avatar-gradient flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-bold text-white ring-2 ring-blue-maat/20 ring-offset-1"
          >
            {initial}
          </div>
        </div>
      </div>
    </header>
  )
}
