import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { SaveStatusBanner } from './SaveStatusBanner'

describe('SaveStatusBanner', () => {
  it('ne s’affiche pas quand aucune sauvegarde n’est en échec', () => {
    render(<SaveStatusBanner hasError={false} onRetry={vi.fn()} />)

    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('affiche un bandeau persistant invitant à ne pas fermer l’onglet en cas d’échec', () => {
    render(<SaveStatusBanner hasError onRetry={vi.fn()} />)

    const banner = screen.getByRole('alert')
    expect(banner.textContent).toMatch(/ne pas fermer/i)
  })

  it('le bouton "Réessayer" déclenche la nouvelle tentative', () => {
    const onRetry = vi.fn()
    render(<SaveStatusBanner hasError onRetry={onRetry} />)

    fireEvent.click(screen.getByRole('button', { name: 'Réessayer' }))

    expect(onRetry).toHaveBeenCalled()
  })
})
