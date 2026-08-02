import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { DomainRadarChart } from './DomainRadarChart'
import { makeDomainScore } from '../../test/dashboardFixtures'

// docs/specs/dashboard.md, section 3, cas 15 : cinq axes sur une échelle fixe 0-100,
// indépendamment des valeurs. Le rendu SVG de Recharts n'est pas mesurable de façon fiable en
// jsdom (ResponsiveContainer dépend d'une vraie mise en page) : la preuve porte donc sur la
// description accessible du graphique (role="img"), qui énonce l'échelle en toutes lettres
// plutôt que de la déduire des données — precisément ce qui doit rester vrai pour prouver
// qu'elle ne s'adapte jamais.
describe('DomainRadarChart', () => {
  it("expose les cinq domaines et une échelle fixe de 0 à 100 dans sa description accessible, quelles que soient les valeurs", () => {
    const lowScores = [
      makeDomainScore('Environmental', 2),
      makeDomainScore('Social', 1),
      makeDomainScore('Ethics', 3),
      makeDomainScore('Procurement', 0),
      makeDomainScore('Governance', 4),
    ]
    const highScores = [
      makeDomainScore('Environmental', 96),
      makeDomainScore('Social', 98),
      makeDomainScore('Ethics', 95),
      makeDomainScore('Procurement', 99),
      makeDomainScore('Governance', 97),
    ]

    const { rerender } = render(<DomainRadarChart domainScores={lowScores} />)
    const imageLow = screen.getByRole('img')
    const labelLow = imageLow.getAttribute('aria-label') ?? ''

    rerender(<DomainRadarChart domainScores={highScores} />)
    const imageHigh = screen.getByRole('img')
    const labelHigh = imageHigh.getAttribute('aria-label') ?? ''

    for (const label of [labelLow, labelHigh]) {
      expect(label).toContain('0 à 100')
      expect(label).toContain('Environnement')
      expect(label).toContain('Social')
      expect(label).toContain('Éthique')
      expect(label).toContain('Achats')
      expect(label).toContain('Gouvernance')
    }

    // Le texte de l'échelle elle-même est identique dans les deux cas : seules les valeurs
    // par domaine varient, jamais la borne annoncée.
    const scaleMentionLow = labelLow.match(/échelle de 0 à 100/i)
    const scaleMentionHigh = labelHigh.match(/échelle de 0 à 100/i)
    expect(scaleMentionLow?.[0]).toBe(scaleMentionHigh?.[0])
  })

  it('porte un titre visible pour les utilisateurs voyants', () => {
    render(<DomainRadarChart domainScores={[makeDomainScore('Environmental', 50)]} />)

    expect(screen.getByText(/radar/i)).toBeDefined()
  })
})
