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
    <Link to="/" className="flex items-center gap-3 px-1">
      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-blue-maat shadow-[0_0_18px_rgba(21,101,255,0.45)] ring-1 ring-blue-maat/60">
        <span className="text-sm font-bold text-white">M</span>
      </div>
      <div>
        <span className="block font-heading text-lg font-bold leading-tight text-white">MAAT</span>
        <span className="block text-xs text-white/50">diagnostic RSE</span>
      </div>
    </Link>
  )
}

function SidebarNav({ onNavigate }: { onNavigate?: () => void }) {
  const inProgressDiagnostic = useDashboardStore((s) => s.inProgressDiagnostic)

  return (
    <div className="flex flex-col gap-5">
      {NAV_SECTIONS.map(({ label, items }) => (
        <div key={label}>
          <p className="mb-1.5 px-3 text-[10px] font-semibold uppercase tracking-widest text-white/40">
            {label}
          </p>
          <ul className="flex flex-col gap-0.5">
            {items.map(({ to, label: itemLabel, icon: Icon, end }) => (
              <motion.li
                key={to}
                initial={{ opacity: 0, x: -8 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{
                  duration: 0.22,
                  ease: 'easeOut',
                  delay: 0.08 + ALL_NAV_ITEMS.findIndex((item) => item.to === to) * 0.05,
                }}
              >
                <NavLink
                  to={to}
                  end={end}
                  onClick={onNavigate}
                  // docs/specs/coquille-et-compte.md, section 3 : active signalé par deux moyens
                  // distincts — fond bg-white/10 + graisse font-semibold (non chromatique).
                  className={({ isActive }) =>
                    `flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm transition-colors duration-150 ${
                      isActive
                        ? 'bg-white/10 font-semibold text-white'
                        : 'font-medium text-white/65 hover:bg-white/5 hover:text-white/90'
                    }`
                  }
                >
                  <Icon size={18} strokeWidth={1.5} aria-hidden="true" />
                  <span className="flex-1">{itemLabel}</span>
                  {to === '/questionnaire' && inProgressDiagnostic && (
                    <Tooltip content="Diagnostic en cours" side="right">
                      <span className="rounded-full bg-blue-maat/30 px-2 py-0.5 text-xs tabular-nums lining-nums text-white">
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
