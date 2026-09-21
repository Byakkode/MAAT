import { memo } from 'react'
import { motion } from 'framer-motion'
import { Building2, Leaf, Scale, ShoppingCart, Users } from 'lucide-react'
import type { RseDomain } from '../../types/questionnaire'
import type { DomainScore } from '../../types/dashboard'
import { DOMAIN_COLORS } from '../../constants/domainColors'

const DOMAIN_CONFIG: Record<RseDomain, { label: string; Icon: typeof Leaf }> = {
  Environmental: { label: 'Environnement', Icon: Leaf },
  Social: { label: 'Social', Icon: Users },
  Ethics: { label: 'Éthique', Icon: Scale },
  Procurement: { label: 'Achats', Icon: ShoppingCart },
  Governance: { label: 'Gouvernance', Icon: Building2 },
}

function scoreClasses(score: number): { text: string; bar: string } {
  if (score >= 70) return { text: 'text-green-maat-text', bar: 'bg-green-maat' }
  if (score >= 50) return { text: 'text-orange', bar: 'bg-orange' }
  return { text: 'text-red', bar: 'bg-red' }
}

interface DomainScoreCardProps {
  domainScore: DomainScore
  delay?: number
}

function DomainScoreCardComponent({ domainScore, delay = 0 }: DomainScoreCardProps) {
  const { domain, score, triggeredRecommendationCount } = domainScore
  const { label, Icon } = DOMAIN_CONFIG[domain]
  const rounded = Math.round(score)
  const { text, bar } = scoreClasses(rounded)
  const color = DOMAIN_COLORS[domain]

  return (
    <motion.div
      className="flex flex-col rounded-xl border border-border bg-white p-4 shadow-card"
      initial={{ opacity: 0, y: 6 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.22, ease: 'easeOut', delay }}
    >
      <div className="mb-3 flex items-center gap-2">
        <div
          className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg"
          style={{ backgroundColor: `color-mix(in srgb, ${color} 14%, transparent)` }}
          aria-hidden="true"
        >
          <Icon size={13} style={{ color }} strokeWidth={1.75} aria-hidden />
        </div>
        <p className="truncate text-[11px] font-semibold uppercase tracking-[0.07em] text-text-muted">
          {label}
        </p>
      </div>

      <div className="mb-2.5 flex items-baseline gap-1">
        <span className={`text-[1.875rem] font-bold leading-none tabular-nums lining-nums ${text}`}>
          {rounded}
        </span>
        <span className="text-[13px] font-medium text-text-muted">/100</span>
      </div>

      <div className="h-1.5 w-full overflow-hidden rounded-full bg-border">
        <motion.div
          className={`h-full rounded-full ${bar}`}
          initial={{ width: 0 }}
          animate={{ width: `${rounded}%` }}
          transition={{ duration: 0.55, ease: 'easeOut', delay: delay + 0.15 }}
        />
      </div>

      <p className="mt-2 text-[11px] text-text-muted">
        {triggeredRecommendationCount > 0
          ? `${triggeredRecommendationCount} recommandation${triggeredRecommendationCount > 1 ? 's' : ''}`
          : 'Aucune recommandation'}
      </p>
    </motion.div>
  )
}

export const DomainScoreCard = memo(DomainScoreCardComponent)
