import { useEffect, useMemo } from 'react'
import { useParams } from 'react-router-dom'
import { ProgressIndicator } from '../components/questionnaire/ProgressIndicator'
import { QuestionStep } from '../components/questionnaire/QuestionStep'
import { SaveStatusBanner } from '../components/questionnaire/SaveStatusBanner'
import { StepNav } from '../components/questionnaire/StepNav'
import { estimateRemainingMinutes } from '../lib/estimateRemainingTime'
import { selectAnsweredCount, selectHasSaveError, selectTotalQuestions, useQuestionnaireStore } from '../store/questionnaireStore'

function formatCompletionDate(iso: string | null): string {
  if (!iso) {
    return ''
  }
  return new Date(iso).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })
}

export function QuestionnairePage() {
  const { diagnosticId } = useParams<{ diagnosticId?: string }>()

  const load = useQuestionnaireStore((s) => s.load)
  const loadStatus = useQuestionnaireStore((s) => s.loadStatus)
  const loadError = useQuestionnaireStore((s) => s.loadError)
  const steps = useQuestionnaireStore((s) => s.steps)
  const currentStepIndex = useQuestionnaireStore((s) => s.currentStepIndex)
  const isEditable = useQuestionnaireStore((s) => s.isEditable)
  const diagnosticStatus = useQuestionnaireStore((s) => s.diagnosticStatus)
  const diagnosticCompletedAt = useQuestionnaireStore((s) => s.diagnosticCompletedAt)
  const startedAt = useQuestionnaireStore((s) => s.startedAt)
  const answeredCount = useQuestionnaireStore(selectAnsweredCount)
  const totalQuestions = useQuestionnaireStore(selectTotalQuestions)
  const hasSaveError = useQuestionnaireStore(selectHasSaveError)
  const completeStatus = useQuestionnaireStore((s) => s.completeStatus)
  const completeError = useQuestionnaireStore((s) => s.completeError)
  const missingQuestionCodes = useQuestionnaireStore((s) => s.missingQuestionCodes)
  const nextStep = useQuestionnaireStore((s) => s.nextStep)
  const prevStep = useQuestionnaireStore((s) => s.prevStep)
  const retryFailedSaves = useQuestionnaireStore((s) => s.retryFailedSaves)
  const completeDiagnostic = useQuestionnaireStore((s) => s.completeDiagnostic)

  useEffect(() => {
    void load(diagnosticId)
  }, [load, diagnosticId])

  const estimatedMinutesRemaining = useMemo(
    () => (isEditable && startedAt ? estimateRemainingMinutes(startedAt, Date.now(), answeredCount, totalQuestions) : null),
    [isEditable, startedAt, answeredCount, totalQuestions],
  )

  if (loadStatus === 'idle' || loadStatus === 'loading') {
    return (
      <p role="status" className="text-text-muted">
        Chargement du questionnaire…
      </p>
    )
  }

  if (loadStatus === 'error') {
    return (
      <p role="alert" className="text-red">
        {loadError}
      </p>
    )
  }

  const currentStep = steps[currentStepIndex]
  if (!currentStep) {
    return (
      <p role="status" className="text-text-muted">
        Aucune question active.
      </p>
    )
  }

  const isLastStep = currentStepIndex === steps.length - 1

  return (
    <section className="rounded-card border border-border bg-white p-5 shadow-card">
      <h1 className="mb-2 text-2xl font-semibold text-text">Questionnaire</h1>

      {completeStatus === 'completed' ? (
        <p role="status" className="text-green-maat">
          Diagnostic complété. Vos réponses ont été enregistrées.
        </p>
      ) : (
        <>
          {!isEditable && (
            <p role="status" className="mb-4 text-sm text-text-muted">
              {diagnosticStatus === 'Archived'
                ? 'Diagnostic abandonné.'
                : `Diagnostic terminé le ${formatCompletionDate(diagnosticCompletedAt)}.`}
            </p>
          )}

          <ProgressIndicator
            currentStepIndex={currentStepIndex}
            totalSteps={steps.length}
            answeredCount={answeredCount}
            totalQuestions={totalQuestions}
            estimatedMinutesRemaining={estimatedMinutesRemaining}
          />

          {isEditable && <SaveStatusBanner hasError={hasSaveError} onRetry={retryFailedSaves} />}

          <QuestionStep domain={currentStep.domain} questions={currentStep.questions} />

          {isEditable ? (
            <>
              {completeStatus === 'error' && (
                <p role="alert" className="mb-4 text-sm text-red">
                  {completeError}
                  {missingQuestionCodes.length > 0 && ` (${missingQuestionCodes.join(', ')})`}
                </p>
              )}
              <StepNav
                canGoPrev={currentStepIndex > 0}
                canGoNext={!hasSaveError}
                isLastStep={isLastStep}
                canComplete={answeredCount === totalQuestions && !hasSaveError}
                completing={completeStatus === 'completing'}
                onPrev={prevStep}
                onNext={nextStep}
                onComplete={() => void completeDiagnostic()}
              />
            </>
          ) : (
            steps.length > 1 && (
              <div className="mt-4 flex justify-between">
                <button
                  type="button"
                  onClick={prevStep}
                  disabled={currentStepIndex === 0}
                  className="rounded-button border border-blue-maat px-4 py-2 font-medium text-blue-maat shadow-button disabled:opacity-40"
                >
                  Précédent
                </button>
                <button
                  type="button"
                  onClick={nextStep}
                  disabled={isLastStep}
                  className="rounded-button border border-blue-maat px-4 py-2 font-medium text-blue-maat shadow-button disabled:opacity-40"
                >
                  Suivant
                </button>
              </div>
            )
          )}
        </>
      )}
    </section>
  )
}
