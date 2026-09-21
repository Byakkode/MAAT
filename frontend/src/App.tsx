import { useEffect } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AppShell } from './components/shell/AppShell'
import { ProtectedRoute } from './components/ProtectedRoute'
import { AccountPage } from './pages/AccountPage'
import { DashboardPage } from './pages/DashboardPage'
import { LoginPage } from './pages/LoginPage'
import { PlanActionsPage } from './pages/PlanActionsPage'
import { QuestionnairePage } from './pages/QuestionnairePage'
import { RapportPage } from './pages/RapportPage'
import { RegisterPage } from './pages/RegisterPage'
import { IndicatorsPage } from './pages/IndicatorsPage'
import { SupportPage } from './pages/SupportPage'
import { useAuthStore } from './store/authStore'

function App() {
  const restoreSession = useAuthStore((state) => state.restoreSession)

  // docs/specs/auth-securite-rgpd.md, section 2 : l'access token vit en mémoire, donc
  // perdu à chaque rechargement — un refresh silencieux au démarrage restaure la session
  // depuis le cookie HttpOnly avant que les routes protégées ne s'affichent.
  useEffect(() => {
    restoreSession()
  }, [restoreSession])

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route element={<ProtectedRoute />}>
          {/* docs/specs/coquille-et-compte.md, section 1 : la coquille (barre latérale,
              en-tête) entoure ce sous-arbre de routes et ne se remonte jamais entre deux
              d'entre elles. */}
          <Route element={<AppShell />}>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/questionnaire/:diagnosticId?" element={<QuestionnairePage />} />
            <Route path="/plan-actions" element={<PlanActionsPage />} />
            <Route path="/rapport" element={<RapportPage />} />
            <Route path="/indicateurs" element={<IndicatorsPage />} />
            <Route path="/compte" element={<AccountPage />} />
            <Route path="/support" element={<SupportPage />} />
          </Route>
        </Route>
      </Routes>
    </BrowserRouter>
  )
}

export default App
