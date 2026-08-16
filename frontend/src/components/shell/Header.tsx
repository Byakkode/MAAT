import { LogOut, Menu } from 'lucide-react'
import { Link } from 'react-router-dom'
import { useAuthStore } from '../../store/authStore'
import { useCurrentUserStore } from '../../store/currentUserStore'

// docs/specs/coquille-et-compte.md, section 4 : "Le rôle est affiché en toutes lettres [...]
// un utilisateur qui se voit refuser une action doit pouvoir comprendre pourquoi." Les valeurs
// brutes (Admin/User/Viewer) sont les noms de MAAT.Domain.Enums.UserRole, jamais montrés tels
// quels à l'écran.
const ROLE_LABELS: Record<string, string> = {
  Admin: 'Administrateur',
  User: 'Utilisateur',
  Viewer: 'Lecteur',
}

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
