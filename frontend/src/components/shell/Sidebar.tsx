import { useRef } from 'react'
import { BarChart2, CheckCircle, ClipboardList, FileText, Settings, X } from 'lucide-react'
import { Link, NavLink } from 'react-router-dom'
import { useDashboardStore } from '../../store/dashboardStore'
import { useFocusTrap } from './useFocusTrap'

const NAV_ITEMS: Array<{ to: string; label: string; icon: typeof BarChart2; end?: boolean }> = [
  { to: '/', label: 'Tableau de bord', icon: BarChart2, end: true },
  { to: '/questionnaire', label: 'Diagnostic', icon: ClipboardList },
  { to: '/plan-actions', label: "Plan d'actions", icon: CheckCircle },
  { to: '/rapport', label: 'Rapports', icon: FileText },
  { to: '/compte', label: 'Mon compte', icon: Settings },
]

function Wordmark() {
  return (
    <Link to="/" className="flex items-center gap-3 px-1">
      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white/20">
        <span className="text-sm font-bold text-white">M</span>
      </div>
      <div>
        <span className="block font-heading text-lg font-bold leading-tight text-white">MAAT</span>
        <span className="block text-xs text-white/60">diagnostic RSE</span>
      </div>
    </Link>
  )
}

function SidebarNav({ onNavigate }: { onNavigate?: () => void }) {
  const inProgressDiagnostic = useDashboardStore((s) => s.inProgressDiagnostic)

  return (
    <ul className="flex flex-col gap-0.5">
      {NAV_ITEMS.map(({ to, label, icon: Icon, end }) => (
        <li key={to}>
          <NavLink
            to={to}
            end={end}
            onClick={onNavigate}
            // docs/specs/coquille-et-compte.md, section 3 : active signalé par bg-white/15
            // + border-white (deux moyens distincts, jamais la seule couleur).
            className={({ isActive }) =>
              `flex items-center gap-3 rounded-button border-l-4 px-3 py-2.5 text-sm font-medium transition-colors ${
                isActive
                  ? 'border-white bg-white/15 text-white'
                  : 'border-transparent text-white/80 hover:bg-white/10 hover:text-white'
              }`
            }
          >
            <Icon size={20} strokeWidth={1.5} aria-hidden="true" />
            <span className="flex-1">{label}</span>
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

export function Sidebar({ mobileOpen, onCloseMobile }: SidebarProps) {
  const panelRef = useRef<HTMLDivElement>(null)
  useFocusTrap(panelRef, mobileOpen, onCloseMobile)

  return (
    <>
      {/* Desktop */}
      <nav
        aria-label="Navigation principale"
        className="hidden w-64 shrink-0 flex-col gap-8 bg-blue-maat px-4 py-6 text-white md:flex"
      >
        <Wordmark />
        <SidebarNav />
      </nav>

      {/* Mobile overlay */}
      {mobileOpen && (
        <div className="fixed inset-0 z-40 md:hidden">
          <div aria-hidden="true" onClick={onCloseMobile} className="absolute inset-0 bg-text/40" />
          <div
            ref={panelRef}
            role="dialog"
            aria-modal="true"
            aria-label="Navigation"
            className="relative flex h-full w-64 flex-col gap-8 bg-blue-maat px-4 py-6 text-white"
          >
            <button
              type="button"
              onClick={onCloseMobile}
              aria-label="Fermer la navigation"
              className="self-end rounded-button p-1.5 text-white hover:bg-white/10"
            >
              <X size={20} strokeWidth={1.5} aria-hidden="true" />
            </button>
            <Wordmark />
            <SidebarNav onNavigate={onCloseMobile} />
          </div>
        </div>
      )}
    </>
  )
}
