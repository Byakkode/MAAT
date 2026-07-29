import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from './ProtectedRoute'
import { useAuthStore } from '../store/authStore'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/login" element={<p>Page de connexion</p>} />
        <Route element={<ProtectedRoute />}>
          <Route path="/questionnaire" element={<p>Contenu protégé</p>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('ProtectedRoute', () => {
  it('affiche un état de chargement pendant la restauration de session, sans rediriger', () => {
    useAuthStore.setState({ status: 'restoring', user: null, error: null })

    renderAt('/questionnaire')

    expect(screen.getByRole('status')).toBeDefined()
    expect(screen.queryByText('Contenu protégé')).toBeNull()
    expect(screen.queryByText('Page de connexion')).toBeNull()
  })

  it('redirige vers /login quand non authentifié', () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null, error: null })

    renderAt('/questionnaire')

    expect(screen.getByText('Page de connexion')).toBeDefined()
    expect(screen.queryByText('Contenu protégé')).toBeNull()
  })

  it('rend la route protégée quand authentifié', () => {
    useAuthStore.setState({
      status: 'authenticated',
      user: { userId: 'u', companyId: 'c', role: 'Admin' },
      error: null,
    })

    renderAt('/questionnaire')

    expect(screen.getByText('Contenu protégé')).toBeDefined()
  })
})
