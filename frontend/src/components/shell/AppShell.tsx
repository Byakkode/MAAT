import { useEffect, useState } from 'react'
import { Outlet } from 'react-router-dom'
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

  return (
    <div className="min-h-screen bg-bg">
      <SkipLink />
      <div className="flex min-h-screen">
        <Sidebar mobileOpen={mobileNavOpen} onCloseMobile={() => setMobileNavOpen(false)} />
        <div className="flex flex-1 flex-col">
          <Header onOpenMobileNav={() => setMobileNavOpen(true)} />
          <VerificationBanner />
          <main id="contenu-principal" tabIndex={-1} className="flex flex-1 flex-col gap-4 p-6">
            <Outlet />
          </main>
        </div>
      </div>
    </div>
  )
}
