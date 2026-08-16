import { Link } from 'react-router-dom'
import type { InProgressDiagnostic } from '../../types/dashboard'

interface InProgressBannerProps {
  diagnostic: InProgressDiagnostic
}

// docs/specs/dashboard.md, section 7 : un bandeau, jamais une page de substitution — il
// coexiste avec le reste du tableau de bord (voir DashboardPage), qu'un diagnostic complété
// existe déjà ou non.
export function InProgressBanner({ diagnostic }: InProgressBannerProps) {
  return (
    <section
      aria-labelledby="in-progress-heading"
      className="mb-4 flex items-center justify-between rounded-card border border-blue-maat bg-white p-4 shadow-card"
    >
      <div>
        <h2 id="in-progress-heading" className="text-base font-semibold text-text">
          Diagnostic en cours
        </h2>
        <p className="text-sm text-text-muted tabular-nums lining-nums">
          {diagnostic.answeredCount} question{diagnostic.answeredCount > 1 ? 's' : ''} répondue
          {diagnostic.answeredCount > 1 ? 's' : ''} sur {diagnostic.totalActiveQuestions}
        </p>
      </div>
      <Link
        to={`/questionnaire/${diagnostic.id}`}
        className="rounded-button bg-blue-maat px-4 py-2 font-medium text-white shadow-button"
      >
        Reprendre le diagnostic
      </Link>
    </section>
  )
}
