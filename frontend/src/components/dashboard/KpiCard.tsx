import { memo } from 'react'
import { motion } from 'framer-motion'
import { TrendingDown, TrendingUp } from 'lucide-react'

export type KpiAccent = 'blue' | 'green' | 'amber' | 'neutral'

interface KpiCardProps {
  title: string
  value: string
  unit?: string
  subtitle?: string
  delta?: number | null
  accent?: KpiAccent
  delay?: number
}

function DeltaPill({ delta }: { delta: number }) {
  if (delta === 0) return <span className="text-[11px] text-text-muted tabular-nums">—</span>
  const isUp = delta > 0
  const abs = Math.abs(Math.round(delta))
  const Icon = isUp ? TrendingUp : TrendingDown
  return (
    <span
      className={`inline-flex shrink-0 items-center gap-0.5 rounded-full px-1.5 py-0.5 text-[11px] font-semibold tabular-nums lining-nums ${
        isUp ? 'bg-green-maat/10 text-green-maat-text' : 'bg-red/10 text-red'
      }`}
    >
      <Icon size={10} strokeWidth={2.5} aria-hidden />
      {isUp ? '+' : '-'}{abs}
    </span>
  )
}

function KpiCardComponent({ title, value, unit, subtitle, delta, delay = 0 }: KpiCardProps) {
  return (
    <motion.div
      className="rounded-xl border border-border bg-white p-5 shadow-card transition-shadow duration-200 hover:shadow-card-hover"
      initial={{ opacity: 0, y: 6 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.22, ease: 'easeOut', delay }}
    >
      <div className="flex items-start justify-between gap-2">
        <p className="text-[11.5px] font-semibold uppercase tracking-[0.08em] text-text-muted">
          {title}
        </p>
        {delta !== null && delta !== undefined && <DeltaPill delta={delta} />}
      </div>

      <div className="mt-3.5">
        <div className="flex items-baseline gap-1.5">
          <span className="text-[2.25rem] font-bold leading-none tracking-tight text-text tabular-nums lining-nums">
            {value}
          </span>
          {unit && (
            <span className="text-sm font-medium text-text-muted">{unit}</span>
          )}
        </div>
        {subtitle && (
          <p className="mt-1.5 text-[13px] text-text-muted">{subtitle}</p>
        )}
      </div>
    </motion.div>
  )
}

export const KpiCard = memo(KpiCardComponent)
