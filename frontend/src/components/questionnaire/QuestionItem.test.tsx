import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { QuestionItem } from './QuestionItem'
import { useQuestionnaireStore } from '../../store/questionnaireStore'
import { makeQuestion, resetQuestionnaireStore } from '../../test/questionnaireFixtures'

const question = makeQuestion('ENV-01', 'Environmental', 1, null, 'Le scope 1 couvre les émissions directes.')

describe('QuestionItem', () => {
  beforeEach(() => {
    resetQuestionnaireStore()
    useQuestionnaireStore.setState({ isEditable: true, responses: { 'ENV-01': null }, saveStatus: {} })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('rend un radiogroup étiqueté par le texte de la question', () => {
    render(<QuestionItem question={question} />)

    expect(screen.getByRole('radiogroup', { name: question.text })).toBeDefined()
  })

  it('n’affiche jamais la valeur numérique, uniquement les libellés de l’échelle (section 3)', () => {
    render(<QuestionItem question={question} />)

    expect(screen.getByRole('radio', { name: "Non, ce n'est pas en place" })).toBeDefined()
    expect(screen.getByRole('radio', { name: 'Pleinement en place et suivi' })).toBeDefined()
    expect(screen.queryByText('0')).toBeNull()
    expect(screen.queryByText('5')).toBeNull()
  })

  it('sélectionner une option appelle setAnswer avec le code et la valeur choisis', () => {
    const setAnswer = vi.fn()
    useQuestionnaireStore.setState({ setAnswer })
    render(<QuestionItem question={question} />)

    fireEvent.click(screen.getByRole('radio', { name: 'Largement déployé' }))

    expect(setAnswer).toHaveBeenCalledWith('ENV-01', 4)
  })

  it('le texte d’aide est replié par défaut et s’affiche au clic', () => {
    render(<QuestionItem question={question} />)

    const details = screen.getByText('Aide').closest('details') as HTMLDetailsElement
    expect(details.open).toBe(false)

    fireEvent.click(screen.getByText('Aide'))

    expect(details.open).toBe(true)
    expect(screen.getByText(question.helpText as string)).toBeDefined()
  })

  it.each([
    ['saving', 'Enregistrement…'],
    ['saved', 'Enregistré'],
    ['error', "Échec de l'enregistrement"],
  ] as const)('annonce l’état de sauvegarde "%s" via une région aria-live, pas seulement par la couleur', (status, text) => {
    useQuestionnaireStore.setState({ saveStatus: { 'ENV-01': status } })
    render(<QuestionItem question={question} />)

    const statusRegion = screen.getByRole('status')
    expect(statusRegion.textContent).toBe(text)
  })

  it('lecture seule : les options sont désactivées et un clic ne déclenche pas de sauvegarde', () => {
    const setAnswer = vi.fn()
    useQuestionnaireStore.setState({ isEditable: false, responses: { 'ENV-01': 3 }, setAnswer })
    render(<QuestionItem question={question} />)

    const option = screen.getByRole('radio', { name: 'En cours de déploiement' }) as HTMLInputElement
    expect(option.disabled).toBe(true)
    expect(option.checked).toBe(true)

    fireEvent.click(option)

    expect(setAnswer).not.toHaveBeenCalled()
  })
})
