interface ProgressIndicatorProps {
  currentStepIndex: number
  totalSteps: number
  answeredCount: number
  totalQuestions: number
  estimatedMinutesRemaining: number | null
}

// docs/specs/questionnaire.md, section 2 et 7 : l'avancement est annoncé via une région
// aria-live, pas seulement affiché visuellement.
export function ProgressIndicator({
  currentStepIndex,
  totalSteps,
  answeredCount,
  totalQuestions,
  estimatedMinutesRemaining,
}: ProgressIndicatorProps) {
  const progressPercent = totalQuestions > 0 ? Math.round((answeredCount / totalQuestions) * 100) : 0

  return (
    <div className="mb-2">
      {/* Indicateurs visuels */}
      <div className="mb-1.5 flex items-center justify-between text-xs text-text-muted tabular-nums">
        <span>
          Étape <strong className="font-semibold text-text">{currentStepIndex + 1}</strong> / {totalSteps}
        </span>
        <span>
          <strong className="font-semibold text-text">{answeredCount}</strong> / {totalQuestions} questions
          {estimatedMinutesRemaining !== null && (
            <> · ~{estimatedMinutesRemaining} min</>
          )}
        </span>
      </div>

      {/* Barre de progression */}
      <div className="h-2 w-full overflow-hidden rounded-full bg-border">
        <div
          className="h-full rounded-full bg-blue-maat transition-all duration-300"
          style={{ width: `${progressPercent}%` }}
          aria-hidden="true"
        />
      </div>

      {/* Texte accessible pour les tests et les lecteurs d'écran */}
      <p role="status" aria-live="polite" className="sr-only">
        Étape {currentStepIndex + 1} sur {totalSteps} · {answeredCount} question(s) répondue(s) sur {totalQuestions}
        {estimatedMinutesRemaining !== null && ` · environ ${estimatedMinutesRemaining} min restantes`}
      </p>
    </div>
  )
}
