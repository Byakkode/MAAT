import { useId } from 'react'
import { QuestionItem } from './QuestionItem'
import { DOMAIN_LABELS } from '../../types/questionnaire'
import type { QuestionAnswer, RseDomain } from '../../types/questionnaire'

interface QuestionStepProps {
  domain: RseDomain
  questions: QuestionAnswer[]
}

// Couleurs par domaine RSE — identiques aux tokens chart-* de index.css pour cohérence visuelle.
const DOMAIN_COLORS: Record<RseDomain, string> = {
  Environmental: '#29CC6A',
  Social:        '#1E88E5',
  Ethics:        '#7E57C2',
  Procurement:   '#FFB74D',
  Governance:    '#42A5F5',
}

export function QuestionStep({ domain, questions }: QuestionStepProps) {
  const headingId = useId()

  return (
    <section aria-labelledby={headingId}>
      <h2 id={headingId} className="mb-4 flex items-center gap-3 text-2xl font-semibold text-text">
        <span
          aria-hidden="true"
          className="inline-block h-7 w-1 shrink-0 rounded-full"
          style={{ backgroundColor: DOMAIN_COLORS[domain] }}
        />
        {DOMAIN_LABELS[domain]}
      </h2>
      {questions.map((question) => (
        <QuestionItem key={question.code} question={question} />
      ))}
    </section>
  )
}
