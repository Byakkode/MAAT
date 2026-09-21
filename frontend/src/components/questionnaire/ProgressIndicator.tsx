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
    <div>
      {/* Indicateurs visuels */}
      <div className="mb-2 flex items-center justify-between gap-3 text-[12px] text-text-muted tabular-nums">
        <span>
          Étape{' '}
          <span className="font-semibold text-text">{currentStepIndex + 1}</span>
          {' '}/ {totalSteps}
        </span>
        <span className="flex items-center gap-2.5">
          {estimatedMinutesRemaining !== null && (
            <span>~{estimatedMinutesRemaining}&nbsp;min</span>
          )}
          <span className="text-[13px] font-semibold text-blue-maat">{progressPercent}&nbsp;%</span>
        </span>
      </div>

      {/* Barre fine */}
      <div className="h-1 w-full overflow-hidden rounded-full bg-border">
        <div
          className="h-full rounded-full bg-blue-maat transition-all duration-500"
          style={{ width: `${progressPercent}%` }}
          aria-hidden="true"
        />
      </div>

      <p className="mt-1.5 text-[12px] text-text-muted tabular-nums">
        <span className="font-medium text-text">{answeredCount}</span>
        {' '}/ {totalQuestions} questions répondues
      </p>

      {/* Texte accessible pour les tests et les lecteurs d'écran */}
      <p role="status" aria-live="polite" className="sr-only">
        Étape {currentStepIndex + 1} sur {totalSteps} · {answeredCount} question(s) répondue(s) sur {totalQuestions}
        {estimatedMinutesRemaining !== null && ` · environ ${estimatedMinutesRemaining} min restantes`}
      </p>
    </div>
  )
}
