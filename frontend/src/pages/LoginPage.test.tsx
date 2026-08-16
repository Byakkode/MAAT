import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

const authApi = vi.hoisted(() => ({
  login: vi.fn(),
  ApiError: class ApiError extends Error {
    status: number
    constructor(message: string, status: number) {
      super(message)
      this.status = status
    }
  },
}))
vi.mock('../api/authApi', () => authApi)

import { LoginPage } from './LoginPage'
import { useAuthStore } from '../store/authStore'

function renderLoginPage() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<p>Tableau de bord</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('LoginPage', () => {
  beforeEach(() => {
    authApi.login.mockReset()
    useAuthStore.setState({ status: 'unauthenticated', user: null, error: null })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('soumet les identifiants saisis et redirige sur succès', async () => {
    authApi.login.mockResolvedValue({
      accessToken: `${btoa('{}')}.${btoa(JSON.stringify({ sub: 'u', company_id: 'c', role: 'Admin' }))}.sig`,
      expiresAt: '2026-01-01T00:15:00Z',
    })

    renderLoginPage()

    fireEvent.change(screen.getByLabelText('Adresse e-mail'), { target: { value: 'admin@entreprise.test' } })
    fireEvent.change(screen.getByLabelText('Mot de passe'), { target: { value: 'MotDePasseValide2026!' } })
    fireEvent.click(screen.getByRole('button', { name: 'Se connecter' }))

    await waitFor(() => expect(screen.getByText('Tableau de bord')).toBeDefined())

    expect(authApi.login).toHaveBeenCalledWith('admin@entreprise.test', 'MotDePasseValide2026!')
  })

  it('affiche le message d’erreur, annoncé, sur échec de connexion', async () => {
    authApi.login.mockRejectedValue(new authApi.ApiError('Identifiants invalides.', 401))

    renderLoginPage()

    fireEvent.change(screen.getByLabelText('Adresse e-mail'), { target: { value: 'a@test.test' } })
    fireEvent.change(screen.getByLabelText('Mot de passe'), { target: { value: 'mauvais-mot-de-passe' } })
    fireEvent.click(screen.getByRole('button', { name: 'Se connecter' }))

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toBe('Identifiants invalides.')
  })

  it('propose un lien vers l’inscription', () => {
    renderLoginPage()

    expect(screen.getByRole('link', { name: /créer un compte/i })).toBeDefined()
  })
})
