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
    <header className="sticky top-0 z-10 flex h-[52px] items-center justify-between gap-4 border-b border-border bg-white px-5">
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

      {/* Droite : identité + actions */}
      <div className="flex items-center gap-1.5">
        {/* Avatar + identité */}
        <div className="flex items-center gap-2.5">
          <div className="hidden flex-col items-end leading-tight sm:flex">
            {email && <span className="max-w-[180px] truncate text-[12px] text-text-muted">{email}</span>}
            {roleLabel && <span className="text-[11px] text-text-muted/70">{roleLabel}</span>}
          </div>
          <div
            aria-hidden
            className="avatar-gradient flex h-8 w-8 shrink-0 cursor-default items-center justify-center rounded-full text-[11px] font-bold text-white"
          >
            {initial}
          </div>
        </div>

        <span aria-hidden className="mx-0.5 h-4 w-px bg-border" />

        <Link
          to="/compte"
          aria-label="Mon compte"
          className="flex h-8 w-8 items-center justify-center rounded-lg text-text-muted transition-colors hover:bg-bg hover:text-text"
        >
          <Settings size={15} strokeWidth={1.5} aria-hidden="true" />
        </Link>

        <button
          type="button"
          onClick={() => void logout()}
          aria-label="Déconnexion"
          className="flex h-8 w-8 items-center justify-center rounded-lg text-text-muted transition-colors hover:bg-bg hover:text-text"
        >
          <LogOut size={15} strokeWidth={1.5} aria-hidden="true" />
        </button>
      </div>
    </header>
  )
}
