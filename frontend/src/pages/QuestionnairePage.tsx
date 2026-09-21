import { useEffect, useMemo } from 'react'
import { ClipboardList } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { DomainStepper } from '../components/questionnaire/DomainStepper'
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
  const goToStep = useQuestionnaireStore((s) => s.goToStep)
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
      <div className="mx-auto max-w-lg">
        <Card>
          <div className="px-2 py-4 text-center">
            <div className="mx-auto mb-5 flex h-14 w-14 items-center justify-center rounded-2xl bg-blue-maat/10">
              <ClipboardList className="h-7 w-7 text-blue-maat" aria-hidden="true" />
            </div>
            <h1 className="mb-2 text-xl font-semibold text-text">Questionnaire RSE</h1>
            <p className="mb-1 text-[13px] text-text-muted">
              Vous n&apos;avez pas de diagnostic en cours.
            </p>
            <p className="mb-6 text-[13px] text-text-muted">
              Répondez à 45 questions en environ 30 minutes pour obtenir votre score RSE
              pondéré par secteur.
            </p>
            {startStatus === 'error' && (
              <p role="alert" className="mb-4 text-sm text-red">
                {startError}
              </p>
            )}
            <button
              type="button"
              onClick={() => void startDiagnostic()}
              disabled={startStatus === 'starting'}
              className="rounded-xl bg-blue-maat px-6 py-2.5 text-[13.5px] font-semibold text-white shadow-button-primary transition-opacity disabled:opacity-50"
            >
              {startStatus === 'starting' ? 'Démarrage…' : 'Démarrer un diagnostic'}
            </button>
          </div>
        </Card>
      </div>
    )
  }

  // section 1, cas 2 : 409 de POST /api/diagnostics — un diagnostic InProgress existe déjà
  // (créé entre-temps, ex. dans un autre onglet). Les deux options explicites prévues par la
  // spec, jamais un message d'erreur nu.
  if (loadStatus === 'conflict') {
    return (
      <div className="mx-auto max-w-lg">
        <Card>
          <h1 className="mb-2 text-xl font-semibold text-text">Questionnaire RSE</h1>
          <p className="mb-4 text-[13px] text-text-muted">Un diagnostic est déjà en cours.</p>
          {startStatus === 'error' && (
            <p role="alert" className="mb-4 text-sm text-red">
              {startError}
            </p>
          )}
          <div className="flex gap-3">
            <Link
              to={`/questionnaire/${conflictDiagnosticId}`}
              className="rounded-xl bg-blue-maat px-5 py-2.5 text-[13.5px] font-semibold text-white shadow-button-primary"
            >
              Reprendre
            </Link>
            <button
              type="button"
              onClick={() => void abandonAndRestart()}
              disabled={startStatus === 'starting'}
              className="rounded-xl border border-red px-5 py-2.5 text-[13.5px] font-medium text-red disabled:opacity-50"
            >
              {startStatus === 'starting' ? 'Abandon…' : 'Abandonner et recommencer'}
            </button>
          </div>
        </Card>
      </div>
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
      <div className="mx-auto max-w-lg">
        <Card>
          <div className="px-2 py-4 text-center">
            <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-green-maat/10">
              <ClipboardList className="h-7 w-7 text-green-maat-text" aria-hidden="true" />
            </div>
            <h1 className="mb-2 text-xl font-semibold text-text">Questionnaire RSE</h1>
            <p role="status" className="text-[13px] text-green-maat-text">
              Diagnostic complété. Vos réponses ont été enregistrées.
            </p>
          </div>
        </Card>
      </div>
    )
  }

  const stepperItems = steps.map((s) => ({ domain: s.domain, questionCount: s.questions.length }))

  function scrollToTop() {
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  function handleNext() {
    nextStep()
    scrollToTop()
  }

  function handlePrev() {
    prevStep()
    scrollToTop()
  }

  function handleStepClick(index: number) {
    goToStep(index)
    scrollToTop()
  }

  return (
    <div className="flex flex-col gap-5">
      {/* En-tête : titre + statut lecture seule + barre de progression */}
      <Card>
        <div className="mb-3 flex items-center justify-between gap-4">
          <h1 className="text-xl font-semibold text-text">Questionnaire RSE</h1>
          {!isEditable && (
            <p role="status" className="text-[13px] text-text-muted">
              {diagnosticStatus === 'Archived'
                ? 'Diagnostic abandonné.'
                : `Diagnostic terminé le ${formatCompletionDate(diagnosticCompletedAt)}.`}
            </p>
          )}
        </div>

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

      {/* Mise en page deux colonnes sur les grands écrans */}
      <div className="xl:grid xl:grid-cols-[200px_1fr] xl:items-start xl:gap-6">
        {/* Stepper de domaine — visible uniquement sur xl */}
        <aside className="hidden xl:block">
          <div className="rounded-xl border border-border bg-white p-4 shadow-card">
            <DomainStepper
              steps={stepperItems}
              currentStepIndex={currentStepIndex}
              onStepClick={handleStepClick}
            />
          </div>
        </aside>

        {/* Contenu principal */}
        <div className="flex flex-col gap-4">
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
              onPrev={handlePrev}
              onNext={handleNext}
              onComplete={() => void completeDiagnostic()}
            />
          ) : (
            steps.length > 1 && (
              <div className="flex justify-between gap-4">
                <button
                  type="button"
                  onClick={handlePrev}
                  disabled={currentStepIndex === 0}
                  className="rounded-xl border border-border bg-white px-5 py-2.5 text-[13.5px] font-medium text-text shadow-card transition-colors hover:border-border-strong hover:bg-bg disabled:opacity-40"
                >
                  Précédent
                </button>
                <button
                  type="button"
                  onClick={handleNext}
                  disabled={isLastStep}
                  className="rounded-xl bg-blue-maat px-6 py-2.5 text-[13.5px] font-semibold text-white shadow-button-primary transition-opacity disabled:opacity-40"
                >
                  Suivant
                </button>
              </div>
            )
          )}
        </div>
      </div>
    </div>
  )
}
