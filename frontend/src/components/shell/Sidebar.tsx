import { useRef } from 'react'
import { BarChart2, CheckCircle, ClipboardList, FileText, Settings, X } from 'lucide-react'
import { Link, NavLink } from 'react-router-dom'
import { useDashboardStore } from '../../store/dashboardStore'
import { useFocusTrap } from './useFocusTrap'

// docs/specs/coquille-et-compte.md, section 3 : cinq entrées, visibles pour tous les rôles —
// ce sont les actions à l'intérieur des écrans qui se désactivent selon le rôle, jamais les
// entrées de navigation elles-mêmes.
const NAV_ITEMS: Array<{ to: string; label: string; icon: typeof BarChart2; end?: boolean }> = [
  { to: '/', label: 'Tableau de bord', icon: BarChart2, end: true },
  { to: '/questionnaire', label: 'Diagnostic', icon: ClipboardList },
  { to: '/plan-actions', label: "Plan d'actions", icon: CheckCircle },
  { to: '/rapport', label: 'Rapports', icon: FileText },
  { to: '/compte', label: 'Mon compte', icon: Settings },
]

function Wordmark() {
  return (
    <Link to="/" className="block">
      <span className="block font-heading text-2xl font-bold text-white">MAAT</span>
      <span className="block text-sm text-white/70">diagnostic RSE</span>
    </Link>
  )
}

function SidebarNav({ onNavigate }: { onNavigate?: () => void }) {
  const inProgressDiagnostic = useDashboardStore((s) => s.inProgressDiagnostic)

  return (
    <ul className="flex flex-col gap-1">
      {NAV_ITEMS.map(({ to, label, icon: Icon, end }) => (
        <li key={to}>
          <NavLink
            to={to}
            end={end}
            onClick={onNavigate}
            // section 3 : l'entrée active se signale par deux moyens distincts, jamais par la
            // seule couleur — un fond (bg-white/15) et une barre verticale à gauche
            // (border-l-4), qui apparaissent et disparaissent ensemble.
            className={({ isActive }) =>
              `flex items-center gap-3 rounded-button border-l-4 px-3 py-2 font-medium ${
                isActive ? 'border-white bg-white/15 text-white' : 'border-transparent text-white/85 hover:bg-white/10'
              }`
            }
          >
            <Icon size={24} strokeWidth={1.5} aria-hidden="true" />
            <span className="flex-1">{label}</span>
            {/* section 3 : "23 / 45" est le rappel le plus utile de la navigation, la promesse
                produit étant de terminer un diagnostic en cours. */}
            {to === '/questionnaire' && inProgressDiagnostic && (
              <span className="rounded-full bg-white/20 px-2 py-0.5 text-xs tabular-nums lining-nums">
                {inProgressDiagnostic.answeredCount} / {inProgressDiagnostic.totalActiveQuestions}
              </span>
            )}
          </NavLink>
        </li>
      ))}
    </ul>
  )
}

interface SidebarProps {
  mobileOpen: boolean
  onCloseMobile: () => void
}

// docs/specs/coquille-et-compte.md, sections 1 et 7 : rendue une fois autour du routeur (le
// composant vit dans AppShell, pas dans chaque écran) ; sous 768 px la colonne fixe se replie
// et un bouton (dans Header) ouvre ce même contenu de navigation en panneau, focus piégé,
// fermeture à Échap (useFocusTrap).
export function Sidebar({ mobileOpen, onCloseMobile }: SidebarProps) {
  const panelRef = useRef<HTMLDivElement>(null)
  useFocusTrap(panelRef, mobileOpen, onCloseMobile)

  return (
    <>
      <nav aria-label="Navigation principale" className="hidden w-64 shrink-0 flex-col gap-6 bg-blue-maat p-4 text-white md:flex">
        <Wordmark />
        <SidebarNav />
      </nav>

      {mobileOpen && (
        <div className="fixed inset-0 z-40 md:hidden">
          <div aria-hidden="true" onClick={onCloseMobile} className="absolute inset-0 bg-text/40" />
          <div
            ref={panelRef}
            role="dialog"
            aria-modal="true"
            aria-label="Navigation"
            className="relative flex h-full w-64 flex-col gap-6 bg-blue-maat p-4 text-white"
          >
            <button
              type="button"
              onClick={onCloseMobile}
              aria-label="Fermer la navigation"
              className="self-end rounded-button p-1 text-white hover:bg-white/10"
            >
              <X size={24} strokeWidth={1.5} aria-hidden="true" />
            </button>
            <Wordmark />
            <SidebarNav onNavigate={onCloseMobile} />
          </div>
        </div>
      )}
    </>
  )
}
