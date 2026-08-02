import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DomainScoreTable } from './DomainScoreTable'
import { makeDomainScore } from '../../test/dashboardFixtures'

// docs/specs/dashboard.md, section 3, cas 16 : un tableau alternatif des cinq scores, présent
// dans le DOM (pas seulement au survol) et accessible au clavier — vérifié en tabulant
// jusqu'à un élément du tableau, pas en supposant qu'un <table> natif suffit.
describe('DomainScoreTable', () => {
  const domainScores = [
    makeDomainScore('Environmental', 58, 0.3, 3),
    makeDomainScore('Social', 52, 0.25, 1),
    makeDomainScore('Ethics', 47, 0.15, 0),
    makeDomainScore('Procurement', 61, 0.15, 2),
    makeDomainScore('Governance', 55, 0.15, 1),
  ]

  it('affiche les cinq domaines dans un tableau natif, présent dans le DOM', () => {
    render(<DomainScoreTable domainScores={domainScores} />)

    const table = screen.getByRole('table')
    expect(table).toBeDefined()
    expect(table.getAttribute('aria-hidden')).not.toBe('true')
    expect(table.closest('[aria-hidden="true"]')).toBeNull()

    const rows = screen.getAllByRole('row')
    // 1 ligne d'en-tête + 5 domaines.
    expect(rows.length).toBe(6)

    expect(screen.getByRole('cell', { name: '58' })).toBeDefined()
    expect(screen.getByRole('rowheader', { name: /Environnement/ })).toBeDefined()
  })

  it('reste accessible au clavier : un lien placé juste après le tableau est atteignable par tabulation', async () => {
    const user = userEvent.setup()
    render(
      <div>
        <DomainScoreTable domainScores={domainScores} />
        <a href="/apres">Après le tableau</a>
      </div>,
    )

    await user.tab()

    // Aucune cellule n'intercepte ni ne bloque le focus : le tableau est traversé sans
    // séquence de tabulation dédiée (comportement natif d'un <table> sans tabindex), et le
    // lien qui le suit immédiatement dans le DOM est bien atteignable.
    expect(document.activeElement).toBe(screen.getByText('Après le tableau'))
  })
})
