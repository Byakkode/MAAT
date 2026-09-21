import { useRef } from 'react'
import { motion } from 'framer-motion'
import { BarChart2, CheckCircle, ClipboardList, FileText, Settings, X } from 'lucide-react'
import { Link, NavLink } from 'react-router-dom'
import { Tooltip } from '../ui/Tooltip'
import { useDashboardStore } from '../../store/dashboardStore'
import { useFocusTrap } from './useFocusTrap'

type NavItem = { to: string; label: string; icon: typeof BarChart2; end?: boolean }

const NAV_SECTIONS: Array<{ label: string; items: NavItem[] }> = [
  {
    label: 'Analyse',
    items: [
      { to: '/', label: 'Tableau de bord', icon: BarChart2, end: true },
      { to: '/questionnaire', label: 'Diagnostic', icon: ClipboardList },
    ],
  },
  {
    label: 'Pilotage',
    items: [
      { to: '/plan-actions', label: "Plan d'actions", icon: CheckCircle },
      { to: '/rapport', label: 'Rapports', icon: FileText },
    ],
  },
  {
    label: 'Compte',
    items: [{ to: '/compte', label: 'Mon compte', icon: Settings }],
  },
]

// Index global des items nav pour le stagger — calculé une fois hors du rendu.
const ALL_NAV_ITEMS = NAV_SECTIONS.flatMap((s) => s.items)

function Wordmark() {
  return (
    <Link to="/" className="flex items-center gap-3 px-2">
      <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-blue-maat">
        <span className="text-sm font-bold text-white">M</span>
      </div>
      <div>
        <span className="block text-[15px] font-bold leading-tight tracking-tight text-white">MAAT</span>
        <span className="block text-[11px] text-white/40">Diagnostic RSE</span>
      </div>
    </Link>
  )
}

function SidebarNav({ onNavigate }: { onNavigate?: () => void }) {
  const inProgressDiagnostic = useDashboardStore((s) => s.inProgressDiagnostic)

  return (
    <div className="flex flex-col gap-6">
      {NAV_SECTIONS.map(({ label, items }) => (
        <div key={label}>
          <p className="mb-1 px-2 text-[10px] font-medium uppercase tracking-[0.12em] text-white/30">
            {label}
          </p>
          <ul className="flex flex-col gap-0.5">
            {items.map(({ to, label: itemLabel, icon: Icon, end }) => (
              <motion.li
                key={to}
                initial={{ opacity: 0, x: -6 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{
                  duration: 0.2,
                  ease: 'easeOut',
                  delay: 0.06 + ALL_NAV_ITEMS.findIndex((item) => item.to === to) * 0.04,
                }}
              >
                <NavLink
                  to={to}
                  end={end}
                  onClick={onNavigate}
                  // docs/specs/coquille-et-compte.md, section 3 : active signalé par deux moyens
                  // distincts — fond bg-white/9 + texte blanc pleine opacité.
                  className={({ isActive }) =>
                    `flex items-center gap-2.5 rounded-xl px-2.5 py-2 text-[13px] transition-all duration-150 ${
                      isActive
                        ? 'bg-white/[0.09] font-medium text-white'
                        : 'font-normal text-white/50 hover:bg-white/[0.05] hover:text-white/80'
                    }`
                  }
                >
                  <Icon size={16} strokeWidth={1.75} aria-hidden="true" />
                  <span className="flex-1">{itemLabel}</span>
                  {to === '/questionnaire' && inProgressDiagnostic && (
                    <Tooltip content="Diagnostic en cours" side="right">
                      <span className="rounded-full bg-blue-maat/25 px-2 py-0.5 text-[11px] tabular-nums lining-nums text-blue-maat/90">
                        {inProgressDiagnostic.answeredCount} / {inProgressDiagnostic.totalActiveQuestions}
                      </span>
                    </Tooltip>
                  )}
                </NavLink>
              </motion.li>
            ))}
          </ul>
        </div>
      ))}
    </div>
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
        className="hidden w-60 shrink-0 flex-col gap-7 sidebar-gradient px-3 py-5 text-white md:flex"
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
            className="relative flex h-full w-60 flex-col gap-7 sidebar-gradient px-3 py-5 text-white"
          >
            <button
              type="button"
              onClick={onCloseMobile}
              aria-label="Fermer la navigation"
              className="self-end rounded-lg p-1.5 text-white/70 hover:bg-white/10 hover:text-white transition-colors"
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
