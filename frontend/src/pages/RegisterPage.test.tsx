import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

const authApi = vi.hoisted(() => ({
  register: vi.fn(),
  ApiError: class ApiError extends Error {
    status: number
    constructor(message: string, status: number) {
      super(message)
      this.status = status
    }
  },
}))
vi.mock('../api/authApi', () => authApi)

import { RegisterPage } from './RegisterPage'
import { useAuthStore } from '../store/authStore'

function renderRegisterPage() {
  return render(
    <MemoryRouter initialEntries={['/register']}>
      <Routes>
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/login" element={<p>Page de connexion</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

function fillMandatoryFields() {
  fireEvent.change(screen.getByLabelText('Adresse e-mail'), { target: { value: 'admin@entreprise.test' } })
  fireEvent.change(screen.getByLabelText('Mot de passe'), { target: { value: 'MotDePasseValide2026!' } })
  fireEvent.change(screen.getByLabelText("Nom de l'entreprise"), { target: { value: 'Entreprise Test' } })
  fireEvent.change(screen.getByLabelText('Code NAF'), { target: { value: '6201Z' } })
  fireEvent.change(screen.getByLabelText('Région'), { target: { value: 'Île-de-France' } })
}

describe('RegisterPage', () => {
  beforeEach(() => {
    authApi.register.mockReset()
    useAuthStore.setState({ status: 'unauthenticated', user: null, error: null })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('soumet les champs saisis, y compris la tranche d’effectif par défaut', async () => {
    authApi.register.mockResolvedValue({ message: 'Vérifiez votre boîte mail pour confirmer votre inscription.' })

    renderRegisterPage()
    fillMandatoryFields()
    fireEvent.click(screen.getByRole('button', { name: "S'inscrire" }))

    await waitFor(() => expect(authApi.register).toHaveBeenCalled())

    expect(authApi.register).toHaveBeenCalledWith({
      email: 'admin@entreprise.test',
      password: 'MotDePasseValide2026!',
      companyName: 'Entreprise Test',
      sectorCode: '6201Z',
      sizeRange: 'Micro',
      region: 'Île-de-France',
    })
  })

  it('affiche le message générique renvoyé par le serveur après inscription (anti-énumération)', async () => {
    authApi.register.mockResolvedValue({ message: 'Vérifiez votre boîte mail pour confirmer votre inscription.' })

    renderRegisterPage()
    fillMandatoryFields()
    fireEvent.click(screen.getByRole('button', { name: "S'inscrire" }))

    expect(await screen.findByText('Vérifiez votre boîte mail pour confirmer votre inscription.')).toBeDefined()
  })

  it('affiche une erreur annoncée si le mot de passe est rejeté', async () => {
    authApi.register.mockRejectedValue(new authApi.ApiError('Le mot de passe doit contenir au moins 12 caractères.', 400))

    renderRegisterPage()
    fillMandatoryFields()
    fireEvent.click(screen.getByRole('button', { name: "S'inscrire" }))

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toBe('Le mot de passe doit contenir au moins 12 caractères.')
  })

  it('propose un lien vers la connexion', () => {
    renderRegisterPage()

    expect(screen.getByRole('link', { name: /se connecter/i })).toBeDefined()
  })
})
