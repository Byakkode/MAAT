import { BookOpen, Clock, Search } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../api/authApi'
import {
  type DocumentationSearchResult,
  type KnowledgeCategory,
  type KnowledgeLevel,
  searchDocumentation,
} from '../api/documentationApi'
import { useEntitlements } from '../billing/entitlements'
import { UpgradeNotice } from '../components/billing/UpgradeNotice'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { KNOWLEDGE_CATEGORY_LABELS, KNOWLEDGE_LEVEL_LABELS } from '../constants/knowledgeLabels'

// Délai après la dernière frappe avant de chercher : assez court pour paraître instantané,
// assez long pour ne pas envoyer une requête par lettre.
const SEARCH_DELAY_MS = 250

const CHIP = 'rounded-full border px-3 py-1 text-[12.5px] font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-maat/40'
const CHIP_ON = 'border-blue-maat bg-blue-maat text-white'
const CHIP_OFF = 'border-border bg-white text-text-muted hover:border-border-strong hover:text-text'

type Load = { status: 'loading' } | { status: 'error'; message: string } | { status: 'loaded'; result: DocumentationSearchResult }

// docs/specs/documentation.md, section 5 : base documentaire RSE. Recherche, rubriques et
// niveau de lecture ; ouverte à toutes les offres pour le sommaire, la lecture d'un article
// demandant Essential.
export function DocumentationPage() {
  const { canReadDocumentation } = useEntitlements()
  const [query, setQuery] = useState('')
  const [debouncedQuery, setDebouncedQuery] = useState('')
  const [category, setCategory] = useState<KnowledgeCategory | null>(null)
  const [level, setLevel] = useState<KnowledgeLevel | null>(null)
  const [load, setLoad] = useState<Load>({ status: 'loading' })

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedQuery(query), SEARCH_DELAY_MS)
    return () => clearTimeout(timer)
  }, [query])

  // Une recherche plus récente annule la précédente : une réponse lente ne peut jamais
  // écraser les résultats d'une recherche tapée après elle.
  useEffect(() => {
    const controller = new AbortController()
    searchDocumentation({ q: debouncedQuery, category, level }, controller.signal)
      .then((result) => setLoad({ status: 'loaded', result }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        setLoad({ status: 'error', message: error instanceof ApiError ? error.message : 'Impossible de charger la documentation.' })
      })
    return () => controller.abort()
  }, [debouncedQuery, category, level])

  const result = load.status === 'loaded' ? load.result : null
  const total = result?.categories.reduce((sum, c) => sum + c.count, 0) ?? 0
  const isFiltered = debouncedQuery.trim() !== '' || category !== null || level !== null

  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        title="Documentation"
        subtitle="Comprendre la RSE, les obligations qui vous concernent et les bonnes pratiques, avec leurs sources."
      />

      {!canReadDocumentation && (
        <UpgradeNotice compact requiredPlan="Essential" title="Lecture des articles">
          Parcourez librement le sommaire et la recherche.
        </UpgradeNotice>
      )}

      <Card as="section" aria-label="Recherche et filtres" className="flex flex-col gap-4">
        <div className="relative">
          <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" aria-hidden />
          <label htmlFor="documentation-search" className="sr-only">
            Rechercher dans la documentation
          </label>
          <input
            id="documentation-search"
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            maxLength={200}
            placeholder="Rechercher : bilan carbone, CSRD, égalité professionnelle…"
            className="w-full rounded-xl border border-border bg-bg py-2.5 pl-9 pr-3 text-[14px] text-text outline-none placeholder:text-text-muted focus:border-blue-maat focus:ring-2 focus:ring-blue-maat/20"
          />
        </div>

        <div className="flex flex-col gap-2">
          <p className="text-[11px] font-semibold uppercase tracking-[0.07em] text-text-muted">Rubriques</p>
          <div className="flex flex-wrap gap-2" role="group" aria-label="Filtrer par rubrique">
            <button type="button" aria-pressed={category === null} onClick={() => setCategory(null)} className={`${CHIP} ${category === null ? CHIP_ON : CHIP_OFF}`}>
              Toutes{result ? ` (${total})` : ''}
            </button>
            {result?.categories.map(({ category: value, count }) => (
              <button
                key={value}
                type="button"
                aria-pressed={category === value}
                onClick={() => setCategory(category === value ? null : value)}
                className={`${CHIP} ${category === value ? CHIP_ON : CHIP_OFF}`}
              >
                {KNOWLEDGE_CATEGORY_LABELS[value]} ({count})
              </button>
            ))}
          </div>
        </div>

        <div className="flex flex-col gap-2">
          <p className="text-[11px] font-semibold uppercase tracking-[0.07em] text-text-muted">Niveau</p>
          <div className="flex flex-wrap gap-2" role="group" aria-label="Filtrer par niveau">
            {([null, 'Essentials', 'Expert'] as const).map((value) => (
              <button
                key={value ?? 'all'}
                type="button"
                aria-pressed={level === value}
                onClick={() => setLevel(value)}
                className={`${CHIP} ${level === value ? CHIP_ON : CHIP_OFF}`}
              >
                {value === null ? 'Tous' : KNOWLEDGE_LEVEL_LABELS[value]}
              </button>
            ))}
          </div>
        </div>
      </Card>

      {load.status === 'loading' && (
        <p role="status" className="text-sm text-text-muted">
          Chargement…
        </p>
      )}

      {load.status === 'error' && (
        <p role="alert" className="text-sm text-red">
          {load.message}
        </p>
      )}

      {result && (
        <section aria-label="Articles" className="flex flex-col gap-3">
          <p role="status" className="text-[13px] text-text-muted">
            {result.articles.length === 0
              ? 'Aucun article ne correspond à votre recherche.'
              : isFiltered
                ? `${result.articles.length} article${result.articles.length > 1 ? 's' : ''} trouvé${result.articles.length > 1 ? 's' : ''}`
                : `${result.articles.length} article${result.articles.length > 1 ? 's' : ''}`}
          </p>

          <ul className="grid grid-cols-1 gap-3 lg:grid-cols-2">
            {result.articles.map((article) => (
              <li key={article.slug}>
                <Card className="flex h-full flex-col gap-2">
                  <div className="flex flex-wrap items-center gap-2 text-[11.5px] font-medium">
                    <span className="rounded-full bg-kpi-blue px-2 py-0.5 text-blue-maat-text">
                      {KNOWLEDGE_CATEGORY_LABELS[article.category]}
                    </span>
                    <span className="rounded-full border border-border px-2 py-0.5 text-text-muted">
                      {KNOWLEDGE_LEVEL_LABELS[article.level]}
                    </span>
                  </div>
                  <h2 className="text-[15px] font-semibold leading-snug text-text">
                    <Link to={`/documentation/${article.slug}`} className="hover:text-blue-maat-text hover:underline">
                      {article.title}
                    </Link>
                  </h2>
                  <p className="flex-1 text-[13px] leading-relaxed text-text-muted">{article.summary}</p>
                  <p className="flex items-center gap-1.5 text-[12px] text-text-muted">
                    <Clock size={13} aria-hidden />
                    {article.readingMinutes} min de lecture
                  </p>
                </Card>
              </li>
            ))}
          </ul>

          {result.articles.length === 0 && (
            <Card as="div" className="flex flex-col items-center py-10 text-center">
              <BookOpen className="mb-3 h-10 w-10 text-border" aria-hidden />
              <p className="max-w-sm text-sm text-text-muted">
                Essayez un autre mot, ou retirez les filtres de rubrique et de niveau.
              </p>
            </Card>
          )}
        </section>
      )}
    </div>
  )
}
