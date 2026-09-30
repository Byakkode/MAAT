import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { axe } from 'vitest-axe'
import type { VsmeCompleteness, VsmeDisclosure } from '../api/vsmeApi'

const indicatorsApiMock = vi.hoisted(() => ({ getIndicators: vi.fn(), upsertIndicators: vi.fn() }))
vi.mock('../api/indicatorsApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/indicatorsApi')>()),
  ...indicatorsApiMock,
}))

const vsmeApiMock = vi.hoisted(() => ({
  getStatement: vi.fn(),
  saveStatement: vi.fn(),
  getCompleteness: vi.fn(),
  listSites: vi.fn(),
  createSite: vi.fn(),
  updateSite: vi.fn(),
  deleteSite: vi.fn(),
}))
vi.mock('../api/vsmeApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/vsmeApi')>()),
  ...vsmeApiMock,
}))

import { IndicatorsPage } from './IndicatorsPage'
import { EMPTY_STATEMENT } from '../api/vsmeApi'
import { useSubscriptionStore } from '../store/subscriptionStore'
import { ESSENTIAL_ENTITLEMENTS, STARTER_ENTITLEMENTS, makeSubscription } from '../test/subscriptionFixtures'

const CODES: VsmeDisclosure[] = ['B1', 'B2', 'B3', 'B4', 'B5', 'B6', 'B7', 'B8', 'B9', 'B10', 'B11']

function completeness(incomplete: Partial<Record<VsmeDisclosure, string[]>>, isMicro = false): VsmeCompleteness {
  const disclosures = CODES.map((code) => ({
    disclosure: code,
    state: incomplete[code] ? ('Incomplete' as const) : ('Complete' as const),
    missing: incomplete[code] ?? [],
  }))
  const completeCount = disclosures.filter((d) => d.state !== 'Incomplete').length
  return { isCompliant: completeCount === 11, isMicro, completeCount, disclosures }
}

function renderPage() {
  return render(
    <MemoryRouter>
      <IndicatorsPage />
    </MemoryRouter>,
  )
}

// docs/specs/norme-volontaire.md, section 5.
describe('IndicatorsPage', () => {
  beforeEach(() => {
    Object.values(indicatorsApiMock).forEach((fn) => fn.mockReset())
    Object.values(vsmeApiMock).forEach((fn) => fn.mockReset())
    indicatorsApiMock.getIndicators.mockResolvedValue(null)
    vsmeApiMock.getStatement.mockResolvedValue(null)
    vsmeApiMock.listSites.mockResolvedValue([])
    vsmeApiMock.getCompleteness.mockResolvedValue(completeness({ B1: ['Forme juridique', 'Au moins un site'], B3: ['Émissions Scope 2'] }))
    useSubscriptionStore.setState({
      status: 'loaded',
      subscription: makeSubscription({ plan: 'Essential', effectivePlan: 'Essential', entitlements: ESSENTIAL_ENTITLEMENTS }),
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
    useSubscriptionStore.setState({ status: 'idle', subscription: null })
  })

  it('présente les onze informations dans l’ordre de la norme, avec la complétude du serveur', async () => {
    renderPage()

    const banner = await screen.findByText('9 informations sur 11 complètes pour ' + new Date().getFullYear())
    expect(banner).toBeDefined()
    const status = screen.getByRole('status')
    expect(within(status).getByRole('link', { name: /B1 · Base d'établissement du rapport/ })).toBeDefined()
    expect(within(status).getByRole('link', { name: /B3 · Énergie/ })).toBeDefined()

    const headings = screen.getAllByRole('heading', { level: 3 }).map((h) => h.textContent)
    expect(headings.slice(0, 11)).toEqual([
      "Base d'établissement du rapport",
      'Pratiques, politiques et initiatives futures pour une économie plus durable',
      'Énergie et émissions de gaz à effet de serre',
      "Pollution de l'air, de l'eau et du sol",
      'Biodiversité',
      'Eau',
      'Ressources, économie circulaire et gestion des déchets',
      'Effectifs : caractéristiques générales',
      'Effectifs : santé et sécurité',
      'Effectifs : rémunération, négociation collective et formation',
      'Condamnations et amendes pour corruption',
    ])
    expect(screen.getByText('À compléter : Émissions Scope 2')).toBeDefined()
  })

  it('Essential peut saisir : l’enregistrement envoie indicateurs et déclarations, puis relit la complétude', async () => {
    const user = userEvent.setup()
    indicatorsApiMock.upsertIndicators.mockImplementation((_year: number, data: unknown) => Promise.resolve(data))
    vsmeApiMock.saveStatement.mockImplementation((_year: number, data: unknown) => Promise.resolve(data))
    renderPage()

    await user.type(await screen.findByLabelText('Émissions brutes Scope 2'), '6.5')
    await user.type(screen.getByLabelText('Forme juridique'), 'SAS')
    const b2 = screen.getByRole('region', { name: /Pratiques, politiques/ })
    await user.click(within(within(b2).getByRole('group', { name: /Avez-vous des pratiques/ })).getByLabelText('Oui'))

    vsmeApiMock.getCompleteness.mockResolvedValue(completeness({ B1: ['Au moins un site'] }))
    await user.click(screen.getAllByRole('button', { name: 'Enregistrer' })[0])

    await waitFor(() => expect(vsmeApiMock.saveStatement).toHaveBeenCalledTimes(1))
    const year = new Date().getFullYear()
    expect(indicatorsApiMock.upsertIndicators).toHaveBeenCalledWith(year, expect.objectContaining({ scope2LocationTco2e: 6.5 }))
    expect(vsmeApiMock.saveStatement).toHaveBeenCalledWith(year, expect.objectContaining({ legalForm: 'SAS', hasPractices: true }))
    expect(await screen.findByText('10 informations sur 11 complètes pour ' + year)).toBeDefined()
  })

  it('ajout d’un site : le géocodage du serveur est affiché, et la complétude relue', async () => {
    const user = userEvent.setup()
    vsmeApiMock.createSite.mockResolvedValue({
      id: 's-1',
      name: 'Atelier',
      address: 'Lieu-dit introuvable',
      tenure: 'Owned',
      inOrNearSensitiveArea: false,
      sensitiveAreaName: null,
      geocoded: false,
      latitude: null,
      longitude: null,
      geocodedLabel: null,
    })
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Ajouter un site' }))
    await user.type(screen.getByLabelText('Nom du site'), 'Atelier')
    await user.type(screen.getByLabelText('Adresse postale complète'), 'Lieu-dit introuvable')
    await user.click(screen.getByRole('button', { name: 'Enregistrer le site' }))

    expect(await screen.findByText(/Adresse non localisée/)).toBeDefined()
    expect(vsmeApiMock.createSite).toHaveBeenCalledWith(expect.objectContaining({ name: 'Atelier', tenure: 'Owned' }))
    expect(vsmeApiMock.getCompleteness).toHaveBeenCalledTimes(2)
  })

  it('Starter : consultation seule, et l’offre Essential proposée', async () => {
    useSubscriptionStore.setState({
      status: 'loaded',
      subscription: makeSubscription({ plan: 'Starter', effectivePlan: 'Starter', entitlements: STARTER_ENTITLEMENTS }),
    })
    vsmeApiMock.getStatement.mockResolvedValue({ ...EMPTY_STATEMENT, legalForm: 'SARL' })

    renderPage()

    expect(await screen.findByText(/Inclus à partir de l'offre Essential/)).toBeDefined()
    expect((screen.getByLabelText('Forme juridique') as HTMLInputElement).value).toBe('SARL')
    expect((screen.getByLabelText('Forme juridique') as HTMLInputElement).readOnly).toBe(true)
    expect(screen.queryByRole('button', { name: 'Enregistrer' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Ajouter un site' })).toBeNull()
  })

  it('micro-entreprise : les données facultatives sont signalées', async () => {
    vsmeApiMock.getCompleteness.mockResolvedValue(completeness({}, true))

    renderPage()

    expect(await screen.findByText(/l’énergie, les émissions, l’eau et les déchets sont facultatifs/)).toBeDefined()
    expect(screen.getAllByText('Facultatif jusqu’à 10 salariés').length).toBeGreaterThan(3)
    expect(screen.getByText(/déclarera sa conformité/)).toBeDefined()
  })

  it('n’a pas de violation d’accessibilité détectable', async () => {
    const { container } = renderPage()
    await screen.findByRole('status')

    expect(await axe(container)).toHaveNoViolations()
  })
})
