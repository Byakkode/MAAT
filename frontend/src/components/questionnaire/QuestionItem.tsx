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

const SAVE_STATUS_COLOR: Record<SaveStatus, string> = {
  idle: '',
  saving: 'text-text-muted',
  saved: 'text-green-maat-text',
  error: 'text-red',
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
      className="mb-3 rounded-xl border border-border bg-white p-5 shadow-card"
    >
      <legend id={legendId} className="mb-4 px-0 text-[14.5px] font-medium leading-snug text-text">
        {question.text}
      </legend>

      {question.helpText && (
        <details className="mb-4 text-[13px]">
          <summary className="cursor-pointer select-none font-medium text-blue-maat-text hover:underline">
            Aide
          </summary>
          <p className="mt-2 rounded-lg bg-bg px-3 py-2.5 text-text-muted">{question.helpText}</p>
        </details>
      )}

      <div role="radiogroup" aria-labelledby={legendId} className="flex flex-col gap-2">
        {ANSWER_SCALE.map((option) => (
          <label
            key={option.value}
            className={[
              'flex cursor-pointer items-center gap-3 rounded-xl border px-4 py-3 text-[13.5px] transition-colors',
              value === option.value
                ? 'border-blue-maat bg-blue-maat/[0.07] font-medium text-text'
                : 'border-border text-text hover:border-blue-maat/30 hover:bg-blue-maat/[0.03]',
              !isEditable ? 'cursor-not-allowed opacity-75' : '',
            ]
              .filter(Boolean)
              .join(' ')}
          >
            {/* Radio natif sr-only — présent dans le DOM pour les tests et l'accessibilité */}
            <input
              type="radio"
              name={question.code}
              value={option.value}
              checked={value === option.value}
              disabled={!isEditable}
              onChange={() => setAnswer(question.code, option.value)}
              className="sr-only"
            />
            {/* Indicateur visuel personnalisé */}
            <span
              aria-hidden="true"
              className={[
                'flex h-5 w-5 shrink-0 items-center justify-center rounded-full border-[2px] transition-colors',
                value === option.value ? 'border-blue-maat bg-blue-maat' : 'border-border bg-white',
              ].join(' ')}
            >
              {value === option.value && (
                <span className="h-2 w-2 rounded-full bg-white" />
              )}
            </span>
            {option.label}
          </label>
        ))}
      </div>

      <p
        role="status"
        aria-live="polite"
        className={`mt-3 text-xs ${SAVE_STATUS_COLOR[status]}`}
      >
        {SAVE_STATUS_TEXT[status]}
      </p>
    </fieldset>
  )
}

export const QuestionItem = memo(QuestionItemComponent)
