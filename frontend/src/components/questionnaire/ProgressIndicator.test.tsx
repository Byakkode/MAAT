import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ProgressIndicator } from './ProgressIndicator'

describe('ProgressIndicator', () => {
  it('annonce l’étape courante et le nombre de questions répondues dans une région aria-live', () => {
    render(
      <ProgressIndicator
        currentStepIndex={1}
        totalSteps={5}
        answeredCount={12}
        totalQuestions={45}
        estimatedMinutesRemaining={20}
      />,
    )

    const region = screen.getByRole('status')
    expect(region.getAttribute('aria-live')).toBe('polite')
    expect(region.textContent).toContain('Étape 2 sur 5')
    expect(region.textContent).toContain('12 question(s) répondue(s) sur 45')
    expect(region.textContent).toContain('20 min')
  })

  it('n’affiche pas d’estimation tant qu’elle n’est pas calculable', () => {
    render(
      <ProgressIndicator
        currentStepIndex={0}
        totalSteps={5}
        answeredCount={0}
        totalQuestions={45}
        estimatedMinutesRemaining={null}
      />,
    )

    expect(screen.queryByText(/min/)).toBeNull()
  })
})
