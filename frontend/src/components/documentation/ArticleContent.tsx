import { ExternalLink } from 'lucide-react'
import { Children, isValidElement, type ReactNode } from 'react'
import Markdown, { type Components } from 'react-markdown'
import { Link } from 'react-router-dom'
import remarkGfm from 'remark-gfm'
import { headingId } from './articleSections'

function textOf(node: ReactNode): string {
  if (typeof node === 'string' || typeof node === 'number') return String(node)
  if (Array.isArray(node)) return node.map(textOf).join('')
  if (isValidElement<{ children?: ReactNode }>(node)) return textOf(node.props.children)
  return Children.toArray(node).map(textOf).join('')
}

// Un élément HTML par élément Markdown, aux couleurs et tailles de la charte. Aucun HTML brut
// n'est accepté dans le Markdown (comportement par défaut de react-markdown) : le contenu ne
// peut pas injecter de script.
const COMPONENTS: Components = {
  h2: ({ children }) => (
    <h2 id={headingId(textOf(children))} className="mb-3 mt-8 scroll-mt-6 text-[18px] font-semibold text-text first:mt-0">
      {children}
    </h2>
  ),
  h3: ({ children }) => <h3 className="mb-2 mt-6 text-[15px] font-semibold text-text">{children}</h3>,
  p: ({ children }) => <p className="mb-4 text-[14.5px] leading-relaxed text-text">{children}</p>,
  ul: ({ children }) => <ul className="mb-4 list-disc space-y-1.5 pl-5 text-[14.5px] leading-relaxed text-text">{children}</ul>,
  ol: ({ children }) => <ol className="mb-4 list-decimal space-y-1.5 pl-5 text-[14.5px] leading-relaxed text-text">{children}</ol>,
  strong: ({ children }) => <strong className="font-semibold text-text">{children}</strong>,
  blockquote: ({ children }) => (
    <blockquote className="mb-4 rounded-lg border-l-4 border-blue-maat bg-kpi-blue px-4 py-3 text-text [&>p]:mb-0">{children}</blockquote>
  ),
  code: ({ children }) => <code className="rounded bg-bg px-1.5 py-0.5 text-[13px]">{children}</code>,
  // Lien vers un autre article : navigation interne, sans recharger la page. Lien externe
  // (organisme public, guichet d'aide) : nouvel onglet, sans transmettre l'adresse de la page
  // ni un accès à la fenêtre d'origine, signalé par une icône et annoncé aux lecteurs d'écran,
  // comme les sources en bas d'article.
  a: ({ href, children }) =>
    href?.startsWith('/') ? (
      <Link to={href} className="font-medium text-blue-maat-text underline hover:no-underline">
        {children}
      </Link>
    ) : (
      <a href={href} target="_blank" rel="noopener noreferrer" className="font-medium text-blue-maat-text underline hover:no-underline">
        {children}
        <ExternalLink size={12} className="ml-0.5 inline-block align-baseline" aria-hidden />
        <span className="sr-only"> (nouvel onglet)</span>
      </a>
    ),
  // Tableau défilant horizontalement sur petit écran plutôt que de déborder de la page.
  table: ({ children }) => (
    <div className="mb-5 overflow-x-auto rounded-lg border border-border">
      <table className="w-full border-collapse text-left text-[13.5px]">{children}</table>
    </div>
  ),
  thead: ({ children }) => <thead className="bg-bg">{children}</thead>,
  th: ({ children }) => <th className="border-b border-border px-3 py-2 font-semibold text-text">{children}</th>,
  td: ({ children }) => <td className="border-b border-border px-3 py-2 align-top text-text">{children}</td>,
}

export function ArticleContent({ markdown }: { markdown: string }) {
  return (
    <Markdown remarkPlugins={[remarkGfm]} components={COMPONENTS}>
      {markdown}
    </Markdown>
  )
}
