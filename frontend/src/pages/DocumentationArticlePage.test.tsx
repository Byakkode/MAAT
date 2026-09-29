import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { axe } from 'vitest-axe'
import type { Article } from '../api/documentationApi'

const documentationApi = vi.hoisted(() => {
  class ApiError extends Error {
    readonly status: number

    constructor(message: string, status: number) {
      super(message)
      this.status = status
    }
  }
  class ArticleError extends ApiError {
    readonly requiredPlan: string | null

    constructor(message: string, status: number, requiredPlan: string | null) {
      super(message, status)
      this.requiredPlan = requiredPlan
    }
  }
  return { getArticle: vi.fn(), ArticleError }
})
vi.mock('../api/documentationApi', () => documentationApi)

import { DocumentationArticlePage } from './DocumentationArticlePage'

const BODY = `Introduction de l'article.

## Les sept domaines

Voir aussi [Démarrer sa démarche](/documentation/demarrer-sa-demarche-rse) et [la norme](https://www.iso.org/fr/iso-26000.html).

## En résumé

| Colonne A | Colonne B |
| --- | --- |
| Valeur 1 | Valeur 2 |

<script>alert('injection')</script>
`

const ARTICLE: Article = {
  slug: 'quest-ce-que-la-rse',
  title: "Qu'est-ce que la RSE ?",
  summary: 'La définition de la RSE.',
  category: 'GettingStarted',
  level: 'Essentials',
  tags: ['définition'],
  readingMinutes: 4,
  updatedOn: '2026-09-28',
  sources: [{ title: 'Commission européenne, COM(2011) 681', url: 'https://eur-lex.europa.eu/exemple' }],
  body: BODY,
}

function renderAt(slug: string) {
  return render(
    <MemoryRouter initialEntries={[`/documentation/${slug}`]}>
      <Routes>
        <Route path="/documentation/:slug" element={<DocumentationArticlePage />} />
      </Routes>
    </MemoryRouter>,
  )
}

// docs/specs/documentation.md, section 5.
describe('DocumentationArticlePage', () => {
  afterEach(() => {
    documentationApi.getArticle.mockReset()
  })

  it('affiche l’article : titre, niveau, date de vérification, contenu Markdown et sources', async () => {
    documentationApi.getArticle.mockResolvedValue(ARTICLE)

    renderAt('quest-ce-que-la-rse')

    expect(await screen.findByRole('heading', { level: 1, name: "Qu'est-ce que la RSE ?" })).toBeDefined()
    expect(documentationApi.getArticle).toHaveBeenCalledWith('quest-ce-que-la-rse')
    expect(screen.getByText("L'essentiel")).toBeDefined()
    expect(screen.getByText('28 septembre 2026')).toBeDefined()
    expect(screen.getByRole('heading', { level: 2, name: 'Les sept domaines' }).id).toBe('les-sept-domaines')
    expect(within(screen.getByRole('table')).getByText('Valeur 2')).toBeDefined()

    const source = screen.getByRole('link', { name: /Commission européenne/ })
    expect(source.getAttribute('href')).toBe('https://eur-lex.europa.eu/exemple')
    expect(source.getAttribute('rel')).toBe('noopener noreferrer')
  })

  it('liens : internes dans l’application, externes dans un nouvel onglet', async () => {
    documentationApi.getArticle.mockResolvedValue(ARTICLE)

    renderAt('quest-ce-que-la-rse')

    const internal = await screen.findByRole('link', { name: 'Démarrer sa démarche' })
    expect(internal.getAttribute('href')).toBe('/documentation/demarrer-sa-demarche-rse')
    expect(internal.getAttribute('target')).toBeNull()

    // Le nom accessible annonce l'ouverture d'un nouvel onglet (jsdom n'y conserve pas l'espace).
    const external = screen.getByRole('link', { name: /^la norme\s*\(nouvel onglet\)$/ })
    expect(external.getAttribute('target')).toBe('_blank')
    expect(external.getAttribute('rel')).toBe('noopener noreferrer')
  })

  // Le Markdown ne peut pas injecter de HTML : une balise écrite dans un article reste du texte.
  it('n’interprète jamais de HTML brut contenu dans le Markdown', async () => {
    documentationApi.getArticle.mockResolvedValue(ARTICLE)

    const { container } = renderAt('quest-ce-que-la-rse')
    await screen.findByRole('heading', { level: 1 })

    expect(container.querySelector('script')).toBeNull()
  })

  it('Starter : l’invitation à passer à Essential remplace le contenu', async () => {
    documentationApi.getArticle.mockRejectedValue(new documentationApi.ArticleError('Offre requise', 403, 'Essential'))

    renderAt('quest-ce-que-la-rse')

    expect(await screen.findByText('Cet article est réservé aux offres payantes')).toBeDefined()
    expect(screen.getByRole('link', { name: 'Voir les offres' })).toBeDefined()
    expect(screen.getByRole('link', { name: 'Toute la documentation' })).toBeDefined()
  })

  it('article inconnu : un message, et le retour à la documentation', async () => {
    documentationApi.getArticle.mockRejectedValue(new documentationApi.ArticleError("Cet article n'existe pas ou n'est plus disponible.", 404, null))

    renderAt('inconnu')

    expect((await screen.findByRole('alert')).textContent).toMatch(/n'existe pas/)
    expect(screen.getByRole('link', { name: 'Toute la documentation' }).getAttribute('href')).toBe('/documentation')
  })

  it('n’a pas de violation d’accessibilité détectable', async () => {
    documentationApi.getArticle.mockResolvedValue(ARTICLE)

    const { container } = renderAt('quest-ce-que-la-rse')
    await screen.findByRole('heading', { level: 1 })

    expect(await axe(container)).toHaveNoViolations()
  })
})
