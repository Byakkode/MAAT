import { apiFetch } from './httpClient'
import { ApiError } from './authApi'

// docs/specs/documentation.md, section 4.
export type KnowledgeCategory =
  | 'GettingStarted'
  | 'Regulation'
  | 'Environment'
  | 'Social'
  | 'BusinessEthics'
  | 'Procurement'
  | 'Governance'
  | 'Standards'
  | 'Funding'

export type KnowledgeLevel = 'Essentials' | 'Expert'

export interface ArticleSummary {
  slug: string
  title: string
  summary: string
  category: KnowledgeCategory
  level: KnowledgeLevel
  tags: string[]
  readingMinutes: number
  // aaaa-mm-jj : date de dernière vérification des sources.
  updatedOn: string
}

export interface ArticleSource {
  title: string
  url: string
}

export interface Article extends ArticleSummary {
  sources: ArticleSource[]
  // Markdown, rendu par react-markdown (jamais injecté comme HTML).
  body: string
}

export interface DocumentationSearchResult {
  // Nombre d'articles par rubrique, sur toute la base (indépendant de la recherche en cours).
  categories: { category: KnowledgeCategory; count: number }[]
  articles: ArticleSummary[]
}

export interface DocumentationQuery {
  q?: string
  category?: KnowledgeCategory | null
  level?: KnowledgeLevel | null
}

// Erreur de lecture d'un article : 403 plan_required (Starter) ou 404, que l'écran distingue.
export class ArticleError extends ApiError {
  readonly requiredPlan: string | null

  constructor(message: string, status: number, requiredPlan: string | null) {
    super(message, status)
    this.requiredPlan = requiredPlan
  }
}

async function readBody(response: Response): Promise<{ message?: string; code?: string; requiredPlan?: string } | null> {
  return (await response.json().catch(() => null)) as { message?: string; code?: string; requiredPlan?: string } | null
}

export async function searchDocumentation(query: DocumentationQuery, signal?: AbortSignal): Promise<DocumentationSearchResult> {
  const params = new URLSearchParams()
  if (query.q?.trim()) params.set('q', query.q.trim())
  if (query.category) params.set('category', query.category)
  if (query.level) params.set('level', query.level)
  const search = params.toString()

  const response = await apiFetch(`/api/documentation${search ? `?${search}` : ''}`, { signal })
  if (!response.ok) {
    const body = await readBody(response)
    throw new ApiError(body?.message ?? 'Impossible de charger la documentation.', response.status)
  }
  return (await response.json()) as DocumentationSearchResult
}

export async function getArticle(slug: string): Promise<Article> {
  const response = await apiFetch(`/api/documentation/${encodeURIComponent(slug)}`)
  if (!response.ok) {
    const body = await readBody(response)
    const message =
      response.status === 404 ? "Cet article n'existe pas ou n'est plus disponible." : body?.message ?? "Impossible de charger l'article."
    throw new ArticleError(message, response.status, body?.code === 'plan_required' ? body.requiredPlan ?? null : null)
  }
  return (await response.json()) as Article
}
