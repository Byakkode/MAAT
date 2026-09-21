import { TrendingDown, TrendingUp, Minus } from 'lucide-react'
import { motion } from 'framer-motion'

type KpiAccent = 'blue' | 'green' | 'amber' | 'neutral'

const ACCENT: Record<KpiAccent, { dot: string; trend: string }> = {
  blue:    { dot: 'bg-blue-maat',   trend: 'text-blue-maat' },
  green:   { dot: 'bg-green-maat',  trend: 'text-green-maat-text' },
  amber:   { dot: 'bg-orange',      trend: 'text-amber' },
  neutral: { dot: 'bg-border-strong', trend: 'text-text-muted' },
}

interface KpiCardProps {
  title: string
  value: string | number
  /** Affiché dans un span séparé après la valeur — permet aux tests de cibler la valeur seule. */
  unit?: string
  subtitle?: string
  /** Variation vs diagnostic précédent — positif = vert, négatif = rouge, 0 = muted */
  delta?: number | null
  /** Accent coloré (point + icône tendance) — charte-maat skill, section Mise en page. */
  accent?: KpiAccent
  /** Délai d'entrée en secondes — pour staggers manuels dans un grid parent. */
  delay?: number
}

export function KpiCard({ title, value, unit, subtitle, delta, accent = 'neutral', delay = 0 }: KpiCardProps) {
  const deltaClass =
    delta == null || delta === 0
      ? 'text-text-muted'
      : delta > 0
        ? 'text-green-maat-text'
        : 'text-red'

  const deltaLabel =
    delta != null && delta !== 0
      ? `${delta > 0 ? '+' : ''}${Math.round(delta)} pts vs diagnostic précédent`
      : delta === 0
        ? 'Stable vs diagnostic précédent'
        : null

  const TrendIcon =
    delta != null && delta > 0
      ? TrendingUp
      : delta != null && delta < 0
        ? TrendingDown
        : Minus

  const { dot } = ACCENT[accent]

  return (
    <motion.div
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: 'easeOut', delay }}
      whileHover={{ y: -2, transition: { duration: 0.15, ease: 'easeOut' } }}
      className="group rounded-card border border-border bg-white p-5 shadow-card transition-all duration-200 hover:shadow-[0_4px_16px_rgba(0,0,0,0.09)] hover:border-border-strong"
    >
      {/* En-tête : label + point accent */}
      <div className="flex items-start justify-between gap-2">
        <p className="text-[11px] font-semibold uppercase tracking-wider text-text-muted">{title}</p>
        <span className={`mt-0.5 h-1.5 w-1.5 shrink-0 rounded-full ${dot}`} aria-hidden />
      </div>

      {/* Valeur principale */}
      <p className="mt-3 font-heading tabular-nums lining-nums leading-none">
        <span className="text-[2rem] font-bold tracking-tight text-text">{value}</span>
        {unit && <span className="ml-1.5 text-base font-normal text-text-muted">{unit}</span>}
      </p>

      {/* Sous-titre */}
      {subtitle && <p className="mt-1.5 text-[13px] text-text-muted">{subtitle}</p>}

      {/* Tendance */}
      {deltaLabel && (
        <div className={`mt-3 flex items-center gap-1 text-[11.5px] font-medium ${deltaClass}`}>
          <TrendIcon size={13} strokeWidth={2} aria-hidden />
          <span>{deltaLabel}</span>
        </div>
      )}
    </motion.div>
  )
}
