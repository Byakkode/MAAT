import { beforeEach, describe, expect, it } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { QuestionStep } from './QuestionStep'
import { useQuestionnaireStore } from '../../store/questionnaireStore'
import { ENV_QUESTIONS, resetQuestionnaireStore } from '../../test/questionnaireFixtures'

describe('QuestionStep', () => {
  beforeEach(() => {
    resetQuestionnaireStore()
    useQuestionnaireStore.setState({
      isEditable: true,
      responses: { 'ENV-01': null, 'ENV-02': null },
      saveStatus: {},
      // Simule uniquement la mise à jour synchrone de setAnswer (responses + statut "saving") :
      // ce test vérifie l'isolation du rendu, pas l'anti-rebond ni l'appel réseau, déjà
      // couverts par questionnaireStore.test.ts.
      setAnswer: (code, value) =>
        useQuestionnaireStore.setState((state) => ({
          responses: { ...state.responses, [code]: value },
          saveStatus: { ...state.saveStatus, [code]: 'saving' },
        })),
    })
  })

  it('affiche le libellé du domaine comme titre d’étape', () => {
    render(<QuestionStep domain="Environmental" questions={ENV_QUESTIONS} />)

    expect(screen.getByRole('heading', { name: 'Environnement' })).toBeDefined()
  })

  it('affiche un radiogroup par question de l’étape', () => {
    render(<QuestionStep domain="Environmental" questions={ENV_QUESTIONS} />)

    expect(screen.getByRole('radiogroup', { name: 'Texte ENV-01' })).toBeDefined()
    expect(screen.getByRole('radiogroup', { name: 'Texte ENV-02' })).toBeDefined()
  })

  it('modifier une réponse met à jour son propre statut sans toucher celui des autres questions', () => {
    // Preuve de l'isolation des sélecteurs Zustand (chaque QuestionItem ne s'abonne qu'à son
    // propre code) : ceci tient même sans React.memo, car QuestionStep lui-même n'est abonné
    // à rien et ne se re-rend jamais ici. Le rôle de memo — éviter que les 45 QuestionItem se
    // re-rendent quand le PARENT (QuestionnairePage, abonné à answeredCount/hasSaveError) se
    // re-rend à chaque réponse — est vérifié séparément dans QuestionnairePage.test.tsx, seul
    // niveau où ce parent existe réellement.
    render(<QuestionStep domain="Environmental" questions={ENV_QUESTIONS} />)

    const statusRegions = screen.getAllByRole('status')
    expect(statusRegions.map((r) => r.textContent)).toEqual(['', ''])

    const env01Group = screen.getByRole('radiogroup', { name: 'Texte ENV-01' })
    fireEvent.click(within(env01Group).getByRole('radio', { name: 'Nous y réfléchissons' }))

    const [statusEnv01, statusEnv02] = screen.getAllByRole('status')
    expect(statusEnv01.textContent).toBe('Enregistrement…')
    expect(statusEnv02.textContent).toBe('')
  })
})
