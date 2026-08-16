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
  return (
    <p role="status" aria-live="polite" className="mb-4 text-sm text-text-muted tabular-nums lining-nums">
      Étape {currentStepIndex + 1} sur {totalSteps} · {answeredCount} question(s) répondue(s) sur {totalQuestions}
      {estimatedMinutesRemaining !== null && ` · environ ${estimatedMinutesRemaining} min restantes`}
    </p>
  )
}
