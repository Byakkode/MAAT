import { useId } from 'react'
import { QuestionItem } from './QuestionItem'
import { DOMAIN_LABELS } from '../../types/questionnaire'
import type { QuestionAnswer, RseDomain } from '../../types/questionnaire'

interface QuestionStepProps {
  domain: RseDomain
  questions: QuestionAnswer[]
}

export function QuestionStep({ domain, questions }: QuestionStepProps) {
  const headingId = useId()

  return (
    <section aria-labelledby={headingId}>
      <h2 id={headingId} className="mb-4 text-2xl font-semibold text-text">
        {DOMAIN_LABELS[domain]}
      </h2>
      {questions.map((question) => (
        <QuestionItem key={question.code} question={question} />
      ))}
    </section>
  )
}
