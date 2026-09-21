import { useEffect, useState } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { AnimatePresence, motion, useReducedMotion } from 'framer-motion'
import { useCurrentUserStore } from '../../store/currentUserStore'
import { useDashboardStore } from '../../store/dashboardStore'
import { Header } from './Header'
import { Sidebar } from './Sidebar'
import { SkipLink } from './SkipLink'
import { VerificationBanner } from './VerificationBanner'

// docs/specs/coquille-et-compte.md, section 1 : "La barre latérale et l'en-tête sont rendus
// une fois, autour du routeur." Ce composant entoure l'Outlet des écrans protégés (App.tsx) —
// il ne remonte jamais entre deux navigations, contrairement aux écrans qu'il affiche.
export function AppShell() {
  const [mobileNavOpen, setMobileNavOpen] = useState(false)
  const location = useLocation()
  // prefers-reduced-motion : supprime toute translation et raccourcit le fondu si l'utilisateur
  // a demandé moins de mouvement (WCAG 2.3.3 AA).
  const prefersReduced = useReducedMotion() ?? false

  const currentUserStatus = useCurrentUserStore((s) => s.status)
  const loadCurrentUser = useCurrentUserStore((s) => s.load)
  const dashboardLoadStatus = useDashboardStore((s) => s.loadStatus)
  const loadDashboard = useDashboardStore((s) => s.load)

  useEffect(() => {
    if (currentUserStatus === 'idle') {
      void loadCurrentUser()
    }
  }, [currentUserStatus, loadCurrentUser])

  useEffect(() => {
    // Alimente le badge d'avancement du diagnostic dans la barre latérale (section 3), visible
    // sur tous les écrans — pas seulement depuis DashboardPage, qui recharge par ailleurs les
    // mêmes données à son propre montage (docs/specs/dashboard.md, section 8).
    if (dashboardLoadStatus === 'idle') {
      void loadDashboard()
    }
  }, [dashboardLoadStatus, loadDashboard])

  const pageVariants = {
    initial: { opacity: 0, y: prefersReduced ? 0 : 6 },
    animate: { opacity: 1, y: 0 },
    exit:    { opacity: 0, y: prefersReduced ? 0 : -6 },
  }

  const pageDuration = prefersReduced ? 0.08 : 0.18

  return (
    <div className="min-h-screen bg-bg">
      <SkipLink />
      <div className="flex min-h-screen">
        <Sidebar mobileOpen={mobileNavOpen} onCloseMobile={() => setMobileNavOpen(false)} />
        <div className="flex flex-1 flex-col">
          <Header onOpenMobileNav={() => setMobileNavOpen(true)} />
          <VerificationBanner />
          <main id="contenu-principal" tabIndex={-1} className="flex flex-1 flex-col p-6">
            {/* mode="wait" : la page sortante termine son exit avant que la suivante entre,
                évitant tout chevauchement de mise en page dans le conteneur flex. */}
            <AnimatePresence mode="wait" initial={false}>
              <motion.div
                key={location.pathname}
                variants={pageVariants}
                initial="initial"
                animate="animate"
                exit="exit"
                transition={{ duration: pageDuration, ease: 'easeInOut' }}
                className="flex flex-1 flex-col gap-4"
              >
                <Outlet />
              </motion.div>
            </AnimatePresence>
          </main>
        </div>
      </div>
    </div>
  )
}
