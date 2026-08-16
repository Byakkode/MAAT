import { memo, useId, useRef } from 'react'
import { ANSWER_SCALE } from '../../constants/answerScale'
import { useQuestionnaireStore } from '../../store/questionnaireStore'
import type { QuestionAnswer } from '../../types/questionnaire'
import type { SaveStatus } from '../../store/questionnaireStore'

const SAVE_STATUS_TEXT: Record<SaveStatus, string> = {
  idle: '',
  saving: 'Enregistrement…',
  saved: 'Enregistré',
  error: "Échec de l'enregistrement",
}

interface QuestionItemProps {
  question: QuestionAnswer
}

// docs/specs/questionnaire.md, section 7 : ce composant s'abonne uniquement à sa propre
// réponse et à son propre statut de sauvegarde (jamais à l'objet d'état complet), et est
// mémoïsé — sans cela, modifier une réponse re-rendrait les 45 composants de question.
function QuestionItemComponent({ question }: QuestionItemProps) {
  const legendId = useId()
  const value = useQuestionnaireStore((state) => state.responses[question.code] ?? null)
  const status = useQuestionnaireStore((state) => state.saveStatus[question.code] ?? 'idle')
  const isEditable = useQuestionnaireStore((state) => state.isEditable)
  const setAnswer = useQuestionnaireStore((state) => state.setAnswer)

  // Compteur exposé en DOM uniquement pour vérifier par mutation que ce composant ne se
  // re-rend pas quand une question voisine est modifiée (docs/specs/questionnaire.md,
  // section 7) — sans effet sur le comportement ni l'accessibilité.
  const renderCount = useRef(0)
  renderCount.current += 1

  return (
    <fieldset
      data-render-count={renderCount.current}
      className="mb-4 rounded-card border border-border bg-white p-5 shadow-card"
    >
      <legend id={legendId} className="mb-2 px-1 text-base font-medium text-text">
        {question.text}
      </legend>

      {question.helpText && (
        <details className="mb-3 text-sm text-text-muted">
          <summary className="cursor-pointer text-blue-maat-text">Aide</summary>
          <p className="mt-1">{question.helpText}</p>
        </details>
      )}

      <div role="radiogroup" aria-labelledby={legendId} className="flex flex-col gap-2">
        {ANSWER_SCALE.map((option) => (
          <label key={option.value} className="flex items-center gap-2 text-sm text-text">
            <input
              type="radio"
              name={question.code}
              value={option.value}
              checked={value === option.value}
              disabled={!isEditable}
              onChange={() => setAnswer(question.code, option.value)}
              className="h-4 w-4 accent-blue-maat"
            />
            {option.label}
          </label>
        ))}
      </div>

      <p role="status" aria-live="polite" className="mt-2 text-sm text-text-muted">
        {SAVE_STATUS_TEXT[status]}
      </p>
    </fieldset>
  )
}

export const QuestionItem = memo(QuestionItemComponent)
