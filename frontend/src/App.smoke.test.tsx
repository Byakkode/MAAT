import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import App from './App'

// Preuve que la chaîne Vitest + jsdom + React Testing Library fonctionne : rendu réel
// d'un composant React dans un DOM simulé, interrogé via les rôles ARIA. La coquille
// (barre latérale, en-tête) ne s'affiche que pour une session authentifiée — voir
// AppShell.a11y.test.tsx pour sa navigation — donc cette fumée se contente de l'état initial
// non authentifié, commun à toute visite de l'application.
describe('App (fumée Vitest + React Testing Library)', () => {
  it('affiche le chargement de la session au démarrage', () => {
    render(<App />)

    expect(screen.getByRole('status').textContent).toBe('Chargement de la session…')
  })
})

