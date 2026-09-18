import { useEffect, useMemo } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ProgressIndicator } from '../components/questionnaire/ProgressIndicator'
import { QuestionStep } from '../components/questionnaire/QuestionStep'
import { SaveStatusBanner } from '../components/questionnaire/SaveStatusBanner'
import { StepNav } from '../components/questionnaire/StepNav'
import { Card } from '../components/ui/Card'
import { estimateRemainingMinutes } from '../lib/estimateRemainingTime'
import {
  selectAnsweredCount,
  selectHasSaveError,
  selectTotalQuestions,
  useQuestionnaireStore,
} from '../store/questionnaireStore'

function formatCompletionDate(iso: string | null): string {
  if (!iso) {
    return ''
  }
  return new Date(iso).toLocaleDateString('fr-FR', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  })
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
  const conflictDiagnosticId = useQuestionnaireStore((s) => s.conflictDiagnosticId)
  const startStatus = useQuestionnaireStore((s) => s.startStatus)
  const startError = useQuestionnaireStore((s) => s.startError)
  const startDiagnostic = useQuestionnaireStore((s) => s.startDiagnostic)
  const abandonAndRestart = useQuestionnaireStore((s) => s.abandonAndRestart)

  useEffect(() => {
    void load(diagnosticId)
  }, [load, diagnosticId])

  const estimatedMinutesRemaining = useMemo(
    () =>
      isEditable && startedAt
        ? estimateRemainingMinutes(startedAt, Date.now(), answeredCount, totalQuestions)
        : null,
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

  // docs/specs/questionnaire.md, section 5 : état normal d'un nouvel utilisateur (404 attendu
  // de GET /current), jamais un message d'erreur — invite à démarrer un diagnostic.
  if (loadStatus === 'no-diagnostic') {
    return (
      <Card>
        <h1 className="mb-2 text-2xl font-semibold text-text">Questionnaire</h1>
        <p className="mb-4 text-text-muted">Vous n&apos;avez pas de diagnostic en cours.</p>
        {startStatus === 'error' && (
          <p role="alert" className="mb-4 text-sm text-red">
            {startError}
          </p>
        )}
        <button
          type="button"
          onClick={() => void startDiagnostic()}
          disabled={startStatus === 'starting'}
          className="rounded-button bg-blue-maat px-4 py-2 font-medium text-white shadow-button disabled:opacity-50"
        >
          {startStatus === 'starting' ? 'Démarrage…' : 'Démarrer un diagnostic'}
        </button>
      </Card>
    )
  }

  // section 1, cas 2 : 409 de POST /api/diagnostics — un diagnostic InProgress existe déjà
  // (créé entre-temps, ex. dans un autre onglet). Les deux options explicites prévues par la
  // spec, jamais un message d'erreur nu.
  if (loadStatus === 'conflict') {
    return (
      <Card>
        <h1 className="mb-2 text-2xl font-semibold text-text">Questionnaire</h1>
        <p className="mb-4 text-text-muted">Un diagnostic est déjà en cours.</p>
        {startStatus === 'error' && (
          <p role="alert" className="mb-4 text-sm text-red">
            {startError}
          </p>
        )}
        <div className="flex gap-3">
          <Link
            to={`/questionnaire/${conflictDiagnosticId}`}
            className="rounded-button bg-blue-maat px-4 py-2 font-medium text-white shadow-button"
          >
            Reprendre
          </Link>
          <button
            type="button"
            onClick={() => void abandonAndRestart()}
            disabled={startStatus === 'starting'}
            className="rounded-button border border-red px-4 py-2 font-medium text-red shadow-button disabled:opacity-50"
          >
            {startStatus === 'starting' ? 'Abandon…' : 'Abandonner et recommencer'}
          </button>
        </div>
      </Card>
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

  // Diagnostic complété : message de confirmation, plus aucune interaction disponible.
  if (completeStatus === 'completed') {
    return (
      <Card>
        <h1 className="mb-3 text-2xl font-semibold text-text">Questionnaire</h1>
        <p role="status" className="text-green-maat-text">
          Diagnostic complété. Vos réponses ont été enregistrées.
        </p>
      </Card>
    )
  }

  return (
    <div className="flex flex-col gap-5">
      {/* En-tête : titre + statut lecture seule + barre de progression */}
      <Card>
        <h1 className="mb-3 text-2xl font-semibold text-text">Questionnaire</h1>

        {!isEditable && (
          <p role="status" className="mb-3 text-sm text-text-muted">
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
      </Card>

      {/* Bandeau d'erreur de sauvegarde */}
      {isEditable && <SaveStatusBanner hasError={hasSaveError} onRetry={retryFailedSaves} />}

      {/* Questions de l'étape courante */}
      <QuestionStep domain={currentStep.domain} questions={currentStep.questions} />

      {/* Erreur de complétion */}
      {isEditable && completeStatus === 'error' && (
        <p role="alert" className="text-sm text-red">
          {completeError}
          {missingQuestionCodes.length > 0 && ` (${missingQuestionCodes.join(', ')})`}
        </p>
      )}

      {/* Navigation */}
      {isEditable ? (
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
      ) : (
        steps.length > 1 && (
          <div className="flex justify-between">
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
    </div>
  )
}
