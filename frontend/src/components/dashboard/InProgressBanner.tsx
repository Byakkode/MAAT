import { Link } from 'react-router-dom'
import type { InProgressDiagnostic } from '../../types/dashboard'
import { buttonLinkClass } from '../ui/buttonStyles'

interface InProgressBannerProps {
  diagnostic: InProgressDiagnostic
}

// docs/specs/dashboard.md, section 7 : un bandeau, jamais une page de substitution — il
// coexiste avec le reste du tableau de bord (voir DashboardPage), qu'un diagnostic complété
// existe déjà ou non.
export function InProgressBanner({ diagnostic }: InProgressBannerProps) {
  const progress = Math.round((diagnostic.answeredCount / diagnostic.totalActiveQuestions) * 100)

  return (
    <section
      aria-labelledby="in-progress-heading"
      className="flex items-center justify-between gap-4 rounded-xl border border-border bg-white p-4 shadow-card"
    >
      <div className="flex min-w-0 items-center gap-3">
        {/* Indicateur de progression live */}
        <span className="relative flex h-2.5 w-2.5 shrink-0" aria-hidden>
          <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-blue-maat opacity-60" />
          <span className="relative inline-flex h-2.5 w-2.5 rounded-full bg-blue-maat" />
        </span>

        <div className="min-w-0">
          <h2 id="in-progress-heading" className="text-[13px] font-semibold text-text">
            Diagnostic en cours
          </h2>
          <div className="mt-1.5 flex items-center gap-2.5">
            <div className="h-1 w-28 overflow-hidden rounded-full bg-border">
              <div
                className="h-full rounded-full bg-blue-maat transition-all"
                style={{ width: `${progress}%` }}
              />
            </div>
            <span className="text-[11.5px] text-text-muted tabular-nums lining-nums">
              {diagnostic.answeredCount} / {diagnostic.totalActiveQuestions}
            </span>
          </div>
        </div>
      </div>

      <Link
        to={`/questionnaire/${diagnostic.id}`}
        className={buttonLinkClass('primary', 'sm')}
      >
        Reprendre
      </Link>
    </section>
  )
}
