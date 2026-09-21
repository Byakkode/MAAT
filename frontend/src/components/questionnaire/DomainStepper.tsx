import { CheckCircle2 } from 'lucide-react'
import { DOMAIN_LABELS } from '../../types/questionnaire'
import type { RseDomain } from '../../types/questionnaire'

interface DomainStepItem {
  domain: RseDomain
  questionCount: number
}

interface DomainStepperProps {
  steps: DomainStepItem[]
  currentStepIndex: number
  onStepClick: (index: number) => void
}

const DOMAIN_ACTIVE: Record<RseDomain, { bg: string; text: string; dot: string }> = {
  Environmental: { bg: 'bg-chart-environnement/10', text: 'text-chart-environnement', dot: 'bg-chart-environnement' },
  Social:        { bg: 'bg-chart-social/10',         text: 'text-chart-social',        dot: 'bg-chart-social' },
  Ethics:        { bg: 'bg-chart-ethique/10',        text: 'text-chart-ethique',       dot: 'bg-chart-ethique' },
  Procurement:   { bg: 'bg-chart-achats/10',         text: 'text-chart-achats',        dot: 'bg-chart-achats' },
  Governance:    { bg: 'bg-chart-gouvernance/10',    text: 'text-chart-gouvernance',   dot: 'bg-chart-gouvernance' },
}

export function DomainStepper({ steps, currentStepIndex, onStepClick }: DomainStepperProps) {
  return (
    <nav aria-label="Progression par domaine">
      <p className="mb-3 text-[11px] font-semibold uppercase tracking-[0.1em] text-text-muted">
        Domaines RSE
      </p>
      <ol className="flex flex-col gap-0.5">
        {steps.map((step, index) => {
          const isDone = index < currentStepIndex
          const isCurrent = index === currentStepIndex
          const cls = DOMAIN_ACTIVE[step.domain]

          return (
            <li key={step.domain}>
              <button
                type="button"
                onClick={() => { if (!isCurrent) onStepClick(index) }}
                aria-current={isCurrent ? 'step' : undefined}
                className={`flex w-full items-center gap-2.5 rounded-lg px-3 py-2.5 text-[13px] transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-maat/40 ${
                  isCurrent
                    ? `${cls.bg} font-medium ${cls.text} cursor-default`
                    : isDone
                      ? 'cursor-pointer text-text-muted hover:bg-bg hover:text-text'
                      : 'cursor-pointer text-text-muted/60 hover:bg-bg hover:text-text-muted'
                }`}
              >
                {/* Indicateur d'état */}
                {isDone ? (
                  <CheckCircle2
                    size={14}
                    strokeWidth={2.5}
                    aria-hidden="true"
                    className="shrink-0 text-text-muted"
                  />
                ) : (
                  <span
                    aria-hidden="true"
                    className={`flex h-3.5 w-3.5 shrink-0 items-center justify-center rounded-full ${
                      isCurrent ? `${cls.dot} opacity-80` : 'bg-border'
                    }`}
                  />
                )}

                <span className="flex-1 truncate text-left">{DOMAIN_LABELS[step.domain]}</span>

                <span className="shrink-0 text-[11px] tabular-nums opacity-60">
                  {step.questionCount}
                </span>
              </button>
            </li>
          )
        })}
      </ol>
    </nav>
  )
}
