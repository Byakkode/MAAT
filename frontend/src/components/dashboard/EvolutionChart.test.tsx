import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { EvolutionChart } from './EvolutionChart'
import { makeHistoryPoint } from '../../test/dashboardFixtures'

// docs/specs/dashboard.md, section 4.
describe('EvolutionChart', () => {
  it("cas 17 : un seul point n'affiche pas de courbe, mais une invitation à renouveler le diagnostic", () => {
    render(<EvolutionChart history={[makeHistoryPoint({ globalScore: 60 })]} />)

    expect(screen.queryByRole('img')).toBeNull()
    expect(screen.getByText(/renouveler/i)).toBeDefined()
  })

  it('cas 18 : deux points ou plus affichent la courbe avec l’écart au précédent', () => {
    render(
      <EvolutionChart
        history={[
          makeHistoryPoint({ completedAt: '2026-02-10T09:24:00Z', globalScore: 20, deltaFromPrevious: null }),
          makeHistoryPoint({ completedAt: '2026-05-18T14:22:00Z', globalScore: 60, deltaFromPrevious: 40 }),
        ]}
      />,
    )

    expect(screen.queryByText(/renouveler/i)).toBeNull()
    const image = screen.getByRole('img')
    expect(image.getAttribute('aria-label')).toContain('0 à 100')

    // "+40 points depuis le 10 février 2026" (ou formulation équivalente) : le signe, la
    // valeur et la date du point précédent doivent apparaître quelque part dans l'écran.
    expect(screen.getByText(/\+\s?40/)).toBeDefined()
    expect(screen.getByText(/10 février 2026/i)).toBeDefined()
  })

  it('affiche un écart négatif sans signe plus', () => {
    render(
      <EvolutionChart
        history={[
          makeHistoryPoint({ completedAt: '2026-02-10T09:24:00Z', globalScore: 80, deltaFromPrevious: null }),
          makeHistoryPoint({ completedAt: '2026-05-18T14:22:00Z', globalScore: 65, deltaFromPrevious: -15 }),
        ]}
      />,
    )

    expect(screen.getByText(/-\s?15/)).toBeDefined()
  })

  it("échelle 0-100 fixe, quelles que soient les valeurs (même raison que le radar)", () => {
    const { rerender } = render(
      <EvolutionChart
        history={[
          makeHistoryPoint({ completedAt: '2026-01-01T00:00:00Z', globalScore: 5, deltaFromPrevious: null }),
          makeHistoryPoint({ completedAt: '2026-02-01T00:00:00Z', globalScore: 8, deltaFromPrevious: 3 }),
        ]}
      />,
    )
    const labelLow = screen.getByRole('img').getAttribute('aria-label') ?? ''

    rerender(
      <EvolutionChart
        history={[
          makeHistoryPoint({ completedAt: '2026-01-01T00:00:00Z', globalScore: 90, deltaFromPrevious: null }),
          makeHistoryPoint({ completedAt: '2026-02-01T00:00:00Z', globalScore: 97, deltaFromPrevious: 7 }),
        ]}
      />,
    )
    const labelHigh = screen.getByRole('img').getAttribute('aria-label') ?? ''

    expect(labelLow.match(/0 à 100/)?.[0]).toBe(labelHigh.match(/0 à 100/)?.[0])
  })
})
