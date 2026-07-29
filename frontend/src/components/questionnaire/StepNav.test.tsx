import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { StepNav } from './StepNav'

function renderNav(overrides: Partial<Parameters<typeof StepNav>[0]> = {}) {
  const props = {
    canGoPrev: true,
    canGoNext: true,
    isLastStep: false,
    canComplete: false,
    completing: false,
    onPrev: vi.fn(),
    onNext: vi.fn(),
    onComplete: vi.fn(),
    ...overrides,
  }
  render(<StepNav {...props} />)
  return props
}

describe('StepNav', () => {
  it('le bouton "Précédent" est désactivé à la première étape', () => {
    renderNav({ canGoPrev: false })

    expect(screen.getByRole('button', { name: 'Précédent' })).toHaveProperty('disabled', true)
  })

  it('le bouton "Suivant" est désactivé tant qu’une sauvegarde est en échec', () => {
    renderNav({ canGoNext: false })

    expect(screen.getByRole('button', { name: 'Suivant' })).toHaveProperty('disabled', true)
  })

  it('appelle onNext au clic sur "Suivant"', () => {
    const props = renderNav()

    fireEvent.click(screen.getByRole('button', { name: 'Suivant' }))

    expect(props.onNext).toHaveBeenCalled()
  })

  it('sur la dernière étape, affiche "Terminer" au lieu de "Suivant"', () => {
    renderNav({ isLastStep: true, canComplete: true })

    expect(screen.queryByRole('button', { name: 'Suivant' })).toBeNull()
    const completeButton = screen.getByRole('button', { name: 'Terminer' })
    expect(completeButton).toHaveProperty('disabled', false)
  })

  it('"Terminer" est désactivé tant que toutes les questions n’ont pas de réponse', () => {
    renderNav({ isLastStep: true, canComplete: false })

    expect(screen.getByRole('button', { name: 'Terminer' })).toHaveProperty('disabled', true)
  })
})
