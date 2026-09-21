import { useId } from 'react'
import { Building2, Leaf, Shield, ShoppingBag, Users } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { QuestionItem } from './QuestionItem'
import { DOMAIN_LABELS } from '../../types/questionnaire'
import type { QuestionAnswer, RseDomain } from '../../types/questionnaire'

interface QuestionStepProps {
  domain: RseDomain
  questions: QuestionAnswer[]
}

const DOMAIN_ICONS: Record<RseDomain, LucideIcon> = {
  Environmental: Leaf,
  Social:        Users,
  Ethics:        Shield,
  Procurement:   ShoppingBag,
  Governance:    Building2,
}

// Classes Tailwind dérivées des tokens --color-chart-* de index.css.
const DOMAIN_CLASSES: Record<RseDomain, { bg: string; text: string }> = {
  Environmental: { bg: 'bg-chart-environnement/10', text: 'text-chart-environnement' },
  Social:        { bg: 'bg-chart-social/10',         text: 'text-chart-social' },
  Ethics:        { bg: 'bg-chart-ethique/10',        text: 'text-chart-ethique' },
  Procurement:   { bg: 'bg-chart-achats/10',         text: 'text-chart-achats' },
  Governance:    { bg: 'bg-chart-gouvernance/10',    text: 'text-chart-gouvernance' },
}

export function QuestionStep({ domain, questions }: QuestionStepProps) {
  const headingId = useId()
  const Icon = DOMAIN_ICONS[domain]
  const { bg, text } = DOMAIN_CLASSES[domain]

  return (
    <section aria-labelledby={headingId}>
      <h2
        id={headingId}
        className="mb-5 flex items-center gap-3 text-xl font-semibold leading-tight tracking-tight text-text"
      >
        {/* Icône domaine — aria-hidden : le nom du domaine est porté par le texte du h2 */}
        <span
          aria-hidden="true"
          className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${bg} ${text}`}
        >
          <Icon size={18} strokeWidth={1.75} />
        </span>
        {DOMAIN_LABELS[domain]}
      </h2>

      {questions.map((question) => (
        <QuestionItem key={question.code} question={question} />
      ))}
    </section>
  )
}
