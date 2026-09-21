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
    <div className="flex justify-between gap-4">
      <button
        type="button"
        onClick={onPrev}
        disabled={!canGoPrev}
        className="flex items-center gap-1.5 rounded-xl border border-border bg-white px-5 py-2.5 text-[13.5px] font-medium text-text shadow-card transition-colors hover:border-border-strong hover:bg-bg disabled:opacity-40"
      >
        <ChevronLeft size={15} aria-hidden="true" />
        Précédent
      </button>

      {isLastStep ? (
        <button
          type="button"
          onClick={onComplete}
          disabled={!canComplete || completing}
          className="flex items-center gap-1.5 rounded-xl bg-green-maat px-6 py-2.5 text-[13.5px] font-semibold text-white shadow-button-primary transition-opacity disabled:opacity-40"
        >
          <CheckCircle2 size={15} aria-hidden="true" />
          Terminer
        </button>
      ) : (
        <button
          type="button"
          onClick={onNext}
          disabled={!canGoNext}
          className="flex items-center gap-1.5 rounded-xl bg-blue-maat px-6 py-2.5 text-[13.5px] font-semibold text-white shadow-button-primary transition-opacity disabled:opacity-40"
        >
          Suivant
          <ChevronRight size={15} aria-hidden="true" />
        </button>
      )}
    </div>
  )
}
