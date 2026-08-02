import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { InProgressBanner } from './InProgressBanner'
import { makeInProgressDiagnostic } from '../../test/dashboardFixtures'

// docs/specs/dashboard.md, section 7 : bandeau, pas une page de substitution — un lien de
// reprise vers le questionnaire, avec l'avancement.
describe('InProgressBanner', () => {
  it("affiche l'avancement et un lien de reprise vers le bon diagnostic", () => {
    render(
      <MemoryRouter>
        <InProgressBanner diagnostic={makeInProgressDiagnostic({ id: 'diag-42', answeredCount: 12, totalActiveQuestions: 45 })} />
      </MemoryRouter>,
    )

    expect(screen.getByText(/12/)).toBeDefined()
    expect(screen.getByText(/45/)).toBeDefined()
    const link = screen.getByRole('link', { name: /reprendre/i })
    expect(link.getAttribute('href')).toBe('/questionnaire/diag-42')
  })
})
