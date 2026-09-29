import { ArrowLeft, CalendarCheck, Clock, ExternalLink } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { type Article, ArticleError, getArticle } from '../api/documentationApi'
import { UpgradeNotice } from '../components/billing/UpgradeNotice'
import { ArticleContent } from '../components/documentation/ArticleContent'
import { extractSections } from '../components/documentation/articleSections'
import { Card } from '../components/ui/Card'
import { KNOWLEDGE_CATEGORY_LABELS, KNOWLEDGE_LEVEL_LABELS, formatKnowledgeDate } from '../constants/knowledgeLabels'

type Load =
  | { status: 'loading' }
  | { status: 'plan-required' }
  | { status: 'error'; message: string }
  | { status: 'loaded'; article: Article }

function BackLink() {
  return (
    <Link to="/documentation" className="inline-flex items-center gap-1.5 text-[13px] font-medium text-text-muted hover:text-blue-maat-text">
      <ArrowLeft size={14} aria-hidden />
      Toute la documentation
    </Link>
  )
}

// docs/specs/documentation.md, section 5 : un article — titre, résumé, sommaire, contenu,
// sources et date de vérification. En Starter, l'invitation à passer à Essential remplace le
// contenu (403 plan_required du serveur, seul juge du droit).
export function DocumentationArticlePage() {
  const { slug = '' } = useParams()
  const [load, setLoad] = useState<Load>({ status: 'loading' })

  useEffect(() => {
    let cancelled = false
    setLoad({ status: 'loading' })
    getArticle(slug)
      .then((article) => {
        if (!cancelled) setLoad({ status: 'loaded', article })
      })
      .catch((error: unknown) => {
        if (cancelled) return
        if (error instanceof ArticleError && error.requiredPlan) {
          setLoad({ status: 'plan-required' })
        } else {
          setLoad({ status: 'error', message: error instanceof Error ? error.message : "Impossible de charger l'article." })
        }
      })
    return () => {
      cancelled = true
    }
  }, [slug])

  if (load.status === 'loading') {
    return (
      <div className="flex flex-col gap-4">
        <BackLink />
        <p role="status" className="text-sm text-text-muted">
          Chargement…
        </p>
      </div>
    )
  }

  if (load.status === 'plan-required') {
    return (
      <div className="flex flex-col gap-4">
        <BackLink />
        <UpgradeNotice requiredPlan="Essential" title="Cet article est réservé aux offres payantes">
          La base documentaire (réglementation, bonnes pratiques et référentiels, avec leurs sources) est incluse dans
          l&apos;offre Essential.
        </UpgradeNotice>
      </div>
    )
  }

  if (load.status === 'error') {
    return (
      <div className="flex flex-col gap-4">
        <BackLink />
        <p role="alert" className="text-sm text-red">
          {load.message}
        </p>
      </div>
    )
  }

  const { article } = load
  const sections = extractSections(article.body)

  return (
    <div className="flex flex-col gap-4">
      <BackLink />

      <div className="grid grid-cols-1 gap-5 xl:grid-cols-[minmax(0,1fr)_16rem]">
        <Card as="article" aria-labelledby="article-title" className="px-6 py-6 sm:px-8">
          <header className="mb-6 border-b border-border pb-5">
            <div className="mb-3 flex flex-wrap items-center gap-2 text-[11.5px] font-medium">
              <span className="rounded-full bg-kpi-blue px-2 py-0.5 text-blue-maat-text">{KNOWLEDGE_CATEGORY_LABELS[article.category]}</span>
              <span className="rounded-full border border-border px-2 py-0.5 text-text-muted">{KNOWLEDGE_LEVEL_LABELS[article.level]}</span>
            </div>
            <h1 id="article-title" className="text-[1.6rem] font-semibold leading-tight tracking-tight text-text">
              {article.title}
            </h1>
            <p className="mt-2 text-[15px] leading-relaxed text-text-muted">{article.summary}</p>
            <p className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-[12.5px] text-text-muted">
              <span className="inline-flex items-center gap-1.5">
                <Clock size={13} aria-hidden />
                {article.readingMinutes} min de lecture
              </span>
              <span className="inline-flex items-center gap-1.5">
                <CalendarCheck size={13} aria-hidden />
                {/* Un seul nœud texte : dans un conteneur flex, texte et <time> seraient deux
                    éléments séparés par l'écart du flex en plus de l'espace. */}
                <span>
                  Sources vérifiées le <time dateTime={article.updatedOn}>{formatKnowledgeDate(article.updatedOn)}</time>
                </span>
              </span>
            </p>
          </header>

          <ArticleContent markdown={article.body} />

          <section aria-labelledby="article-sources" className="mt-8 border-t border-border pt-5">
            <h2 id="article-sources" className="mb-3 text-[15px] font-semibold text-text">
              Sources
            </h2>
            <ol className="flex flex-col gap-2 text-[13px]">
              {article.sources.map((source) => (
                <li key={source.url}>
                  <a
                    href={source.url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-start gap-1.5 text-blue-maat-text hover:underline"
                  >
                    <ExternalLink size={13} className="mt-0.5 shrink-0" aria-hidden />
                    {source.title}
                    <span className="sr-only"> (nouvel onglet)</span>
                  </a>
                </li>
              ))}
            </ol>
            <p className="mt-4 text-[12px] text-text-muted">
              Ces contenus donnent des repères et ne constituent pas un avis juridique. En cas de doute sur votre
              situation, rapprochez-vous de votre expert-comptable ou de votre conseil.
            </p>
          </section>
        </Card>

        {sections.length > 1 && (
          <nav aria-label="Sommaire de l'article" className="hidden xl:block">
            <div className="sticky top-6 rounded-card border border-border bg-white p-4">
              <p className="mb-2 text-[11px] font-semibold uppercase tracking-[0.07em] text-text-muted">Dans cet article</p>
              <ol className="flex flex-col gap-1.5 text-[13px]">
                {sections.map((section) => (
                  <li key={section.id}>
                    <a href={`#${section.id}`} className="text-text-muted hover:text-blue-maat-text">
                      {section.title}
                    </a>
                  </li>
                ))}
              </ol>
            </div>
          </nav>
        )}
      </div>
    </div>
  )
}
