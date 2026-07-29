interface StepNavProps {
  canGoPrev: boolean
  canGoNext: boolean
  isLastStep: boolean
  canComplete: boolean
  completing: boolean
  onPrev: () => void
  onNext: () => void
  onComplete: () => void
}

// docs/specs/questionnaire.md, section 4 : le passage à l'étape suivante est bloqué tant
// qu'une sauvegarde est en échec (canGoNext le porte, calculé par la page à partir de
// selectHasSaveError).
export function StepNav({ canGoPrev, canGoNext, isLastStep, canComplete, completing, onPrev, onNext, onComplete }: StepNavProps) {
  return (
    <div className="mt-4 flex justify-between">
      <button
        type="button"
        onClick={onPrev}
        disabled={!canGoPrev}
        className="rounded-button border border-blue-maat px-4 py-2 font-medium text-blue-maat shadow-button disabled:opacity-40"
      >
        Précédent
      </button>
      {isLastStep ? (
        <button
          type="button"
          onClick={onComplete}
          disabled={!canComplete || completing}
          className="rounded-button bg-green-maat px-4 py-2 font-medium text-white shadow-button disabled:opacity-40"
        >
          Terminer
        </button>
      ) : (
        <button
          type="button"
          onClick={onNext}
          disabled={!canGoNext}
          className="rounded-button bg-blue-maat px-4 py-2 font-medium text-white shadow-button disabled:opacity-40"
        >
          Suivant
        </button>
      )}
    </div>
  )
}
