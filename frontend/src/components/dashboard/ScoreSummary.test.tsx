import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ScoreSummary } from './ScoreSummary'

// docs/specs/dashboard.md, section 2.
describe('ScoreSummary', () => {
  it('affiche le score arrondi, son libellé qualitatif et le secteur utilisé pour la pondération', () => {
    render(<ScoreSummary score={62.3} sectorCode="4941A" />)

    expect(screen.getByText('62')).toBeDefined()
    expect(screen.getByText('Démarche structurée')).toBeDefined()
    expect(screen.getByText(/4941A/)).toBeDefined()
  })

  it("un score faible n'est jamais présenté avec une couleur d'échec (rouge)", () => {
    render(<ScoreSummary score={12} sectorCode="4941A" />)

    // "Démarche à initier" doit rester dans le même registre neutre que les autres tranches :
    // aucune classe de couleur d'alerte/erreur sur le nombre lui-même.
    const value = screen.getByText('12')
    expect(value.className).not.toMatch(/text-red|bg-red/)
  })
})
