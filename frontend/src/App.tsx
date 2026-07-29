import { useEffect } from 'react'
import { BrowserRouter, Link, Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from './components/ProtectedRoute'
import { DashboardPage } from './pages/DashboardPage'
import { LoginPage } from './pages/LoginPage'
import { QuestionnairePage } from './pages/QuestionnairePage'
import { RapportPage } from './pages/RapportPage'
import { RegisterPage } from './pages/RegisterPage'
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
      <div className="min-h-screen bg-bg p-6">
        <nav className="mb-4 flex gap-4 font-medium text-blue-maat">
          <Link to="/questionnaire">Questionnaire</Link>
          <Link to="/dashboard">Tableau de bord</Link>
          <Link to="/rapport">Rapport</Link>
        </nav>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route element={<ProtectedRoute />}>
            <Route path="/questionnaire/:diagnosticId?" element={<QuestionnairePage />} />
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/rapport" element={<RapportPage />} />
          </Route>
        </Routes>
      </div>
    </BrowserRouter>
  )
}

export default App
