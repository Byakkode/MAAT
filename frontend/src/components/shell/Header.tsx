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

  return (
    <header className="flex items-center justify-between gap-4 border-b border-border bg-white px-4 py-3">
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={onOpenMobileNav}
          aria-label="Ouvrir la navigation"
          className="rounded-button p-1 text-text hover:bg-bg md:hidden"
        >
          <Menu size={24} strokeWidth={1.5} aria-hidden="true" />
        </button>
        <span className="font-heading font-semibold text-text">{companyName}</span>
      </div>

      <div className="flex items-center gap-4 text-sm text-text-muted">
        <span>{email}</span>
        {role && <span className="font-medium text-text">{ROLE_LABELS[role] ?? role}</span>}
        <Link to="/compte" className="font-medium text-blue-maat-text">
          Mon compte
        </Link>
        <button type="button" onClick={() => void logout()} className="flex items-center gap-1 font-medium text-blue-maat-text">
          <LogOut size={18} strokeWidth={1.5} aria-hidden="true" />
          Déconnexion
        </button>
      </div>
    </header>
  )
}
