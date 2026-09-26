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

function renderRegisterPage(url = '/register') {
  return render(
    <MemoryRouter initialEntries={[url]}>
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
  // Combobox NAF : taper le code exact puis sélectionner le premier résultat au clavier
  const nafInput = screen.getByLabelText("Secteur d'activité (code NAF)")
  fireEvent.change(nafInput, { target: { value: '6201Z' } })
  fireEvent.keyDown(nafInput, { key: 'ArrowDown' })
  fireEvent.keyDown(nafInput, { key: 'Enter' })
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

  // docs/specs/abonnement.md, section 2 : l'offre choisie sur la page d'accueil est
  // enregistrée avec le compte.
  it('transmet l’offre choisie sur la page d’accueil avec l’inscription', async () => {
    authApi.register.mockResolvedValue({ message: 'Vérifiez votre boîte mail pour confirmer votre inscription.' })

    renderRegisterPage('/register?offre=essential&periode=annuelle')
    expect(screen.getByText(/offre choisie/i).textContent).toContain('Essential (annuelle)')
    fillMandatoryFields()
    fireEvent.click(screen.getByRole('button', { name: "S'inscrire" }))

    await waitFor(() => expect(authApi.register).toHaveBeenCalled())
    expect(authApi.register).toHaveBeenCalledWith(expect.objectContaining({ plan: 'Essential', billingPeriod: 'Yearly' }))
  })

  it('ignore une offre qui ne se souscrit pas encore (Enterprise)', async () => {
    authApi.register.mockResolvedValue({ message: 'Vérifiez votre boîte mail pour confirmer votre inscription.' })

    renderRegisterPage('/register?offre=enterprise')
    expect(screen.queryByText(/offre choisie/i)).toBeNull()
    fillMandatoryFields()
    fireEvent.click(screen.getByRole('button', { name: "S'inscrire" }))

    await waitFor(() => expect(authApi.register).toHaveBeenCalled())
    expect(authApi.register.mock.calls[0][0]).not.toHaveProperty('plan')
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
