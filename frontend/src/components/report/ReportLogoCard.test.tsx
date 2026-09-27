import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { axe } from 'vitest-axe'
import { ApiError } from '../../api/authApi'
import { useAuthStore } from '../../store/authStore'
import { useSubscriptionStore } from '../../store/subscriptionStore'
import { ESSENTIAL_ENTITLEMENTS, STARTER_ENTITLEMENTS, makeSubscription } from '../../test/subscriptionFixtures'

const logoApi = vi.hoisted(() => ({
  MAX_LOGO_BYTES: 2 * 1024 * 1024,
  ACCEPTED_LOGO_TYPES: 'image/png,image/jpeg,image/webp',
  fetchCompanyLogo: vi.fn(),
  uploadCompanyLogo: vi.fn(),
  deleteCompanyLogo: vi.fn(),
}))
vi.mock('../../api/companyLogoApi', () => logoApi)

import { ReportLogoCard } from './ReportLogoCard'

function setRole(role: 'Admin' | 'User' | 'Viewer') {
  useAuthStore.setState({ status: 'authenticated', user: { userId: 'u-1', companyId: 'c-1', role }, error: null })
}

function setEntitlements(entitlements: typeof ESSENTIAL_ENTITLEMENTS) {
  useSubscriptionStore.setState({ status: 'loaded', subscription: makeSubscription({ entitlements }) })
}

function renderCard() {
  return render(
    <MemoryRouter>
      <ReportLogoCard />
    </MemoryRouter>,
  )
}

const png = () => new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'logo.png', { type: 'image/png' })

// docs/specs/rapport-pdf.md, section 7.
describe('ReportLogoCard', () => {
  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => 'blob:logo')
    URL.revokeObjectURL = vi.fn()
    setRole('Admin')
    setEntitlements(ESSENTIAL_ENTITLEMENTS)
  })

  afterEach(() => {
    vi.clearAllMocks()
    useSubscriptionStore.setState({ status: 'idle', subscription: null })
  })

  it('Starter : propose l’offre Essential, sans champ d’envoi ni appel réseau', () => {
    setEntitlements(STARTER_ENTITLEMENTS)

    renderCard()

    expect(screen.getByText('Votre logo sur le rapport')).toBeDefined()
    expect(screen.getByText(/offre Essential/)).toBeDefined()
    expect(screen.queryByLabelText('Fichier du logo')).toBeNull()
    expect(logoApi.fetchCompanyLogo).not.toHaveBeenCalled()
  })

  it('sans logo : aperçu vide et bouton d’ajout', async () => {
    logoApi.fetchCompanyLogo.mockResolvedValue(null)

    renderCard()

    expect(await screen.findByText('Aucun logo')).toBeDefined()
    expect(screen.getByRole('button', { name: 'Ajouter un logo' })).toBeDefined()
    expect(screen.queryByRole('button', { name: 'Retirer le logo' })).toBeNull()
  })

  it('envoie le fichier choisi puis affiche le logo enregistré', async () => {
    logoApi.fetchCompanyLogo.mockResolvedValueOnce(null).mockResolvedValueOnce(new Blob(['png']))
    logoApi.uploadCompanyLogo.mockResolvedValue(undefined)

    renderCard()
    await screen.findByText('Aucun logo')
    fireEvent.change(screen.getByLabelText('Fichier du logo'), { target: { files: [png()] } })

    expect(await screen.findByRole('img', { name: 'Logo actuel de votre entreprise' })).toBeDefined()
    expect(logoApi.uploadCompanyLogo).toHaveBeenCalledOnce()
    expect(screen.getByRole('status').textContent).toMatch(/Logo enregistré/)
    expect(screen.getByRole('button', { name: 'Remplacer le logo' })).toBeDefined()
  })

  it('refuse un fichier de plus de 2 Mo avant tout envoi', async () => {
    logoApi.fetchCompanyLogo.mockResolvedValue(null)
    const big = new File([new Uint8Array(2 * 1024 * 1024 + 1)], 'logo.png', { type: 'image/png' })

    renderCard()
    await screen.findByText('Aucun logo')
    fireEvent.change(screen.getByLabelText('Fichier du logo'), { target: { files: [big] } })

    expect((await screen.findByRole('alert')).textContent).toMatch(/2 Mo/)
    expect(logoApi.uploadCompanyLogo).not.toHaveBeenCalled()
  })

  it('affiche le motif du refus renvoyé par le serveur', async () => {
    logoApi.fetchCompanyLogo.mockResolvedValue(null)
    logoApi.uploadCompanyLogo.mockRejectedValue(new ApiError('Format non reconnu : envoyez une image PNG, JPEG ou WebP.', 400))

    renderCard()
    await screen.findByText('Aucun logo')
    fireEvent.change(screen.getByLabelText('Fichier du logo'), { target: { files: [png()] } })

    expect((await screen.findByRole('alert')).textContent).toMatch(/PNG, JPEG ou WebP/)
    expect(screen.getByText('Aucun logo')).toBeDefined()
  })

  it('retire le logo', async () => {
    logoApi.fetchCompanyLogo.mockResolvedValue(new Blob(['png']))
    logoApi.deleteCompanyLogo.mockResolvedValue(undefined)

    renderCard()
    await userEvent.click(await screen.findByRole('button', { name: 'Retirer le logo' }))

    await waitFor(() => expect(screen.getByText('Aucun logo')).toBeDefined())
    expect(logoApi.deleteCompanyLogo).toHaveBeenCalledOnce()
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:logo')
  })

  it('non-administrateur : voit le logo, sans pouvoir le modifier', async () => {
    setRole('Viewer')
    logoApi.fetchCompanyLogo.mockResolvedValue(new Blob(['png']))

    renderCard()

    expect(await screen.findByRole('img', { name: 'Logo actuel de votre entreprise' })).toBeDefined()
    expect(screen.getByText(/Seul un administrateur/)).toBeDefined()
    expect(screen.queryByRole('button')).toBeNull()
  })

  it('n’a pas de violation d’accessibilité détectable', async () => {
    logoApi.fetchCompanyLogo.mockResolvedValue(new Blob(['png']))

    const { container } = renderCard()
    await screen.findByRole('img', { name: 'Logo actuel de votre entreprise' })

    expect(await axe(container)).toHaveNoViolations()
  })
})
