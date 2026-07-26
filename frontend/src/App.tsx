import { BrowserRouter, Link, Route, Routes } from 'react-router-dom'
import { DashboardPage } from './pages/DashboardPage'
import { QuestionnairePage } from './pages/QuestionnairePage'
import { RapportPage } from './pages/RapportPage'

function App() {
  return (
    <BrowserRouter>
      <div className="min-h-screen bg-bg p-6">
        <nav className="mb-4 flex gap-4 font-medium text-blue-maat">
          <Link to="/questionnaire">Questionnaire</Link>
          <Link to="/dashboard">Tableau de bord</Link>
          <Link to="/rapport">Rapport</Link>
        </nav>
        <Routes>
          <Route path="/questionnaire" element={<QuestionnairePage />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/rapport" element={<RapportPage />} />
        </Routes>
      </div>
    </BrowserRouter>
  )
}

export default App
