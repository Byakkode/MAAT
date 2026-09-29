import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { axe } from 'vitest-axe'
import type { ArticleSummary, DocumentationSearchResult } from '../api/documentationApi'
import { useSubscriptionStore } from '../store/subscriptionStore'
import { ESSENTIAL_ENTITLEMENTS, STARTER_ENTITLEMENTS, makeSubscription } from '../test/subscriptionFixtures'

const documentationApi = vi.hoisted(() => ({ searchDocumentation: vi.fn() }))
vi.mock('../api/documentationApi', () => documentationApi)

import { DocumentationPage } from './DocumentationPage'

const article = (overrides: Partial<ArticleSummary>): ArticleSummary => ({
  slug: 'quest-ce-que-la-rse',
  title: "Qu'est-ce que la RSE ?",
  summary: 'La définition de la RSE.',
  category: 'GettingStarted',
  level: 'Essentials',
  tags: ['définition'],
  readingMinutes: 4,
  updatedOn: '2026-09-28',
  ...overrides,
})

const result = (articles: ArticleSummary[]): DocumentationSearchResult => ({
  categories: [
    { category: 'GettingStarted', count: 6 },
    { category: 'Regulation', count: 2 },
  ],
  articles,
})

function renderPage() {
  return render(
    <MemoryRouter>
      <DocumentationPage />
    </MemoryRouter>,
  )
}

// docs/specs/documentation.md, section 5.
describe('DocumentationPage', () => {
  beforeEach(() => {
    useSubscriptionStore.setState({ status: 'loaded', subscription: makeSubscription({ entitlements: ESSENTIAL_ENTITLEMENTS }) })
    documentationApi.searchDocumentation.mockResolvedValue(
      result([article({}), article({ slug: 'csrd', title: 'CSRD et ESRS', category: 'Regulation', level: 'Expert' })]),
    )
  })

  afterEach(() => {
    documentationApi.searchDocumentation.mockReset()
    useSubscriptionStore.setState({ status: 'idle', subscription: null })
  })

  it('liste les articles avec leur rubrique, leur niveau et leur temps de lecture, liens vers chaque article', async () => {
    renderPage()

    const list = await screen.findByRole('list')
    const items = within(list).getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(within(items[0]!).getByRole('link', { name: "Qu'est-ce que la RSE ?" }).getAttribute('href')).toBe('/documentation/quest-ce-que-la-rse')
    expect(within(items[1]!).getByText('Réglementation')).toBeDefined()
    expect(within(items[1]!).getByText('Expert')).toBeDefined()
    expect(within(items[0]!).getByText('4 min de lecture')).toBeDefined()
    expect(screen.getByRole('button', { name: 'Toutes (8)' })).toBeDefined()
    expect(screen.getByRole('button', { name: 'Premiers pas (6)' })).toBeDefined()
  })

  it('cherche après la frappe, avec le texte saisi', async () => {
    renderPage()
    await screen.findByRole('list')

    await userEvent.type(screen.getByLabelText('Rechercher dans la documentation'), 'bilan carbone')

    await waitFor(() =>
      expect(documentationApi.searchDocumentation).toHaveBeenLastCalledWith(
        { q: 'bilan carbone', category: null, level: null },
        expect.any(AbortSignal),
      ),
    )
    // Une seule recherche pour toute la saisie, pas une par lettre.
    expect(documentationApi.searchDocumentation.mock.calls.length).toBeLessThanOrEqual(3)
  })

  it('filtre par rubrique et par niveau, et signale l’état des boutons', async () => {
    renderPage()
    await screen.findByRole('list')

    const regulation = screen.getByRole('button', { name: 'Réglementation (2)' })
    await userEvent.click(regulation)
    await userEvent.click(screen.getByRole('button', { name: 'Expert' }))

    await waitFor(() =>
      expect(documentationApi.searchDocumentation).toHaveBeenLastCalledWith(
        { q: '', category: 'Regulation', level: 'Expert' },
        expect.any(AbortSignal),
      ),
    )
    expect(regulation.getAttribute('aria-pressed')).toBe('true')
  })

  it('aucun résultat : un message et une piste, pas une page vide', async () => {
    documentationApi.searchDocumentation.mockResolvedValue(result([]))

    renderPage()

    expect(await screen.findByText('Aucun article ne correspond à votre recherche.')).toBeDefined()
    expect(screen.getByText(/Essayez un autre mot/)).toBeDefined()
  })

  it('Starter : le sommaire reste visible, avec l’invitation à Essential pour lire les articles', async () => {
    useSubscriptionStore.setState({ status: 'loaded', subscription: makeSubscription({ effectivePlan: 'Starter', entitlements: STARTER_ENTITLEMENTS }) })

    renderPage()

    expect(await screen.findByRole('link', { name: "Qu'est-ce que la RSE ?" })).toBeDefined()
    expect(screen.getByText('Lecture des articles')).toBeDefined()
    expect(screen.getByText(/Inclus à partir de l'offre Essential/)).toBeDefined()
  })

  it('signale une erreur de chargement', async () => {
    documentationApi.searchDocumentation.mockRejectedValue(new Error('réseau'))

    renderPage()

    expect((await screen.findByRole('alert')).textContent).toMatch(/Impossible de charger la documentation/)
  })

  it('n’a pas de violation d’accessibilité détectable', async () => {
    const { container } = renderPage()
    await screen.findByRole('list')

    expect(await axe(container)).toHaveNoViolations()
  })
})
