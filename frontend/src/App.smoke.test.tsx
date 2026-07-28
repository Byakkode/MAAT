import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import App from './App'

// Preuve que la chaîne Vitest + jsdom + React Testing Library fonctionne : rendu réel
// d'un composant React dans un DOM simulé, interrogé via les rôles ARIA.
describe('App (fumée Vitest + React Testing Library)', () => {
  it('rend la navigation principale', () => {
    render(<App />)

    expect(screen.getByRole('link', { name: 'Questionnaire' })).toBeDefined()
    expect(screen.getByRole('link', { name: 'Tableau de bord' })).toBeDefined()
    expect(screen.getByRole('link', { name: 'Rapport' })).toBeDefined()
  })
})

