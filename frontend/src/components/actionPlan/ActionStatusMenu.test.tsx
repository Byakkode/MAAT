import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from 'vitest-axe'
import { ActionStatusMenu } from './ActionStatusMenu'

// docs/specs/recommandations.md, section 4 bis : un clic sur l'étiquette ouvre les quatre
// statuts ; le statut choisi part aussitôt, sans étape intermédiaire.
describe('ActionStatusMenu', () => {
  it('lecture seule : l’étiquette, sans menu', () => {
    render(<ActionStatusMenu status="InProgress" canEdit={false} onChange={vi.fn()} />)

    expect(screen.getByText('En cours')).toBeDefined()
    expect(screen.queryByRole('button')).toBeNull()
  })

  it('ouvre les quatre statuts, le statut courant coché', async () => {
    render(<ActionStatusMenu status="Planned" canEdit onChange={vi.fn()} />)

    await userEvent.click(screen.getByRole('button', { name: 'Statut : Planifié. Changer le statut' }))

    const items = await screen.findAllByRole('menuitemradio')
    expect(items.map((item) => item.textContent)).toEqual(['Planifié', 'En cours', 'Bloqué', 'Terminé'])
    expect(items[0]!.getAttribute('aria-checked')).toBe('true')
  })

  // Le défaut de l'ancien clic cyclique : Planifié → Terminé passait par En cours et Bloqué.
  it('va directement au statut choisi, en un seul changement', async () => {
    const onChange = vi.fn()
    render(<ActionStatusMenu status="Planned" canEdit onChange={onChange} />)

    await userEvent.click(screen.getByRole('button', { name: /Statut : Planifié/ }))
    await userEvent.click(await screen.findByRole('menuitemradio', { name: 'Terminé' }))

    expect(onChange).toHaveBeenCalledOnce()
    expect(onChange).toHaveBeenCalledWith('Done')
    expect(screen.queryByRole('menu')).toBeNull()
  })

  it('choisir le statut déjà en place ne déclenche rien', async () => {
    const onChange = vi.fn()
    render(<ActionStatusMenu status="Blocked" canEdit onChange={onChange} />)

    await userEvent.click(screen.getByRole('button', { name: /Statut : Bloqué/ }))
    await userEvent.click(await screen.findByRole('menuitemradio', { name: 'Bloqué' }))

    expect(onChange).not.toHaveBeenCalled()
  })

  it('au clavier : Entrée ouvre, flèches et Entrée choisissent, Échap ferme', async () => {
    const onChange = vi.fn()
    render(<ActionStatusMenu status="Planned" canEdit onChange={onChange} />)

    screen.getByRole('button', { name: /Statut : Planifié/ }).focus()
    await userEvent.keyboard('{Enter}')
    await screen.findByRole('menu')
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('menu')).toBeNull()

    await userEvent.keyboard('{Enter}')
    await screen.findByRole('menu')
    await userEvent.keyboard('{ArrowDown}{Enter}')

    expect(onChange).toHaveBeenCalledWith('InProgress')
  })

  it('désactivé pendant un enregistrement', () => {
    render(<ActionStatusMenu status="Planned" canEdit disabled onChange={vi.fn()} />)

    expect((screen.getByRole('button', { name: /Statut : Planifié/ }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('n’a pas de violation d’accessibilité détectable, menu ouvert', async () => {
    const { baseElement } = render(<ActionStatusMenu status="Planned" canEdit onChange={vi.fn()} />)

    await userEvent.click(screen.getByRole('button', { name: /Statut : Planifié/ }))
    await screen.findByRole('menu')

    // Règle « region » (bonne pratique axe, pas un critère WCAG) écartée pour ce seul test : le
    // menu, comme toute couche déroulante, est rendu en fin de <body> (portail Radix) pour ne
    // pas être rogné par la carte — donc hors de tout repère <main>, ici comme dans
    // l'application. Toutes les autres règles s'appliquent, menu ouvert compris.
    expect(await axe(baseElement, { rules: { region: { enabled: false } } })).toHaveNoViolations()
  })
})
