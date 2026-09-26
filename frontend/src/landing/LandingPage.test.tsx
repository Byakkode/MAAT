import { describe, expect, it } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from 'vitest-axe'
import { MemoryRouter } from 'react-router-dom'
import { LandingPage } from './LandingPage'

// Les liens de la page sont des <Link> React Router : ils exigent un routeur.
function renderLanding() {
  return render(
    <MemoryRouter>
      <LandingPage />
    </MemoryRouter>,
  )
}

describe('LandingPage', () => {
  it("ne signale aucune violation d'accessibilité détectable statiquement", async () => {
    const { container } = renderLanding()

    expect(await axe(container)).toHaveNoViolations()
  })

  it('a un seul titre de niveau 1', () => {
    renderLanding()

    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
  })

  it("pointe les appels à l'action vers l'inscription de l'application", () => {
    renderLanding()

    const ctas = screen.getAllByRole('link', { name: /commencer le diagnostic|créer mon compte/i })
    expect(ctas.length).toBeGreaterThan(0)
    for (const link of ctas) expect(link.getAttribute('href')).toBe('/register')
  })

  it('recalcule le score global de l’entreprise témoin quand on change de secteur', async () => {
    const user = userEvent.setup()
    renderLanding()
    const section = screen.getByRole('region', { name: /le même effort/i })

    expect(within(section).getByText('61', { selector: '.sr-only' })).toBeTruthy()

    await user.click(within(section).getByRole('button', { name: /commerce/i }))

    expect(within(section).getByRole('button', { name: /commerce/i }).getAttribute('aria-pressed')).toBe('true')
    expect(within(section).getByText('35 %')).toBeTruthy()
    expect(within(section).getByText('60', { selector: '.sr-only' })).toBeTruthy()
  })

  it('compare quatre offres, dont Enterprise en « bientôt disponible » sans lien d’inscription', () => {
    renderLanding()
    const table = screen.getByRole('table', { name: /fonctionnalités incluses/i })

    const plans = within(table).getAllByRole('columnheader')
    expect(plans.map((th) => th.textContent)).toEqual([
      expect.stringContaining('Starter'),
      expect.stringContaining('Essential'),
      expect.stringContaining('Professional'),
      expect.stringContaining('Enterprise'),
    ])
    expect(within(plans[3]).getAllByText('Bientôt disponible').length).toBeGreaterThan(0)
    expect(within(plans[3]).queryByRole('link')).toBeNull()
    for (const plan of plans.slice(0, 3)) expect(within(plan).getByRole('link').getAttribute('href')).toBe('/register')
    expect(within(plans[2]).queryByText('Bientôt disponible')).toBeNull()
  })

  it('annonce inclus / non inclus pour chaque cellule du comparatif', () => {
    renderLanding()
    const table = screen.getByRole('table', { name: /fonctionnalités incluses/i })
    const row = within(table).getByRole('row', { name: /score par domaine/i })

    expect(within(row).getAllByRole('cell').map((td) => td.textContent)).toEqual(['Non inclus', 'Inclus', 'Inclus', 'Inclus'])
  })

  it('parcourt les onglets de la visite produit au clavier', async () => {
    const user = userEvent.setup()
    renderLanding()
    const first = screen.getByRole('tab', { name: /diagnostic/i })

    await user.click(first)
    await user.keyboard('{ArrowRight}')

    const second = screen.getByRole('tab', { name: /recommandations/i })
    expect(second.getAttribute('aria-selected')).toBe('true')
    expect(document.activeElement).toBe(second)
    expect(screen.getByRole('tabpanel', { name: /recommandations/i })).toBeTruthy()
  })
})
