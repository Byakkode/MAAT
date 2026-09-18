import { ChevronLeft, ChevronRight, CheckCircle2 } from 'lucide-react'

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
export function StepNav({
  canGoPrev,
  canGoNext,
  isLastStep,
  canComplete,
  completing,
  onPrev,
  onNext,
  onComplete,
}: StepNavProps) {
  return (
    <div className="mt-4 flex justify-between">
      <button
        type="button"
        onClick={onPrev}
        disabled={!canGoPrev}
        className="flex items-center gap-1.5 rounded-button border border-blue-maat px-4 py-2 font-medium text-blue-maat shadow-button transition-colors hover:bg-blue-maat/5 disabled:opacity-40"
      >
        <ChevronLeft size={16} aria-hidden="true" />
        Précédent
      </button>

      {isLastStep ? (
        <button
          type="button"
          onClick={onComplete}
          disabled={!canComplete || completing}
          className="flex items-center gap-1.5 rounded-button bg-green-maat px-4 py-2 font-medium text-white shadow-button transition-opacity disabled:opacity-40"
        >
          <CheckCircle2 size={16} aria-hidden="true" />
          Terminer
        </button>
      ) : (
        <button
          type="button"
          onClick={onNext}
          disabled={!canGoNext}
          className="flex items-center gap-1.5 rounded-button bg-blue-maat px-4 py-2 font-medium text-white shadow-button transition-opacity disabled:opacity-40"
        >
          Suivant
          <ChevronRight size={16} aria-hidden="true" />
        </button>
      )}
    </div>
  )
}
