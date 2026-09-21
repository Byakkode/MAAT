type KpiAccent = 'blue' | 'green' | 'amber' | 'neutral'

const ACCENT: Record<KpiAccent, { border: string; bg: string }> = {
  blue:    { border: 'border-l-blue-maat',  bg: 'bg-kpi-blue' },
  green:   { border: 'border-l-green-maat', bg: 'bg-kpi-green' },
  amber:   { border: 'border-l-orange',     bg: 'bg-kpi-amber' },
  neutral: { border: 'border-l-border',     bg: 'bg-white' },
}

interface KpiCardProps {
  title: string
  value: string | number
  /** Affiché dans un span séparé après la valeur — permet aux tests de cibler la valeur seule. */
  unit?: string
  subtitle?: string
  /** Variation vs diagnostic précédent — positif = vert, négatif = rouge, 0 = muted */
  delta?: number | null
  /** Bordure gauche colorée + fond teinté — charte-maat skill, section Mise en page. */
  accent?: KpiAccent
}

export function KpiCard({ title, value, unit, subtitle, delta, accent = 'neutral' }: KpiCardProps) {
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

  const { border, bg } = ACCENT[accent]

  return (
    <div
      className={`rounded-card border border-border border-l-4 ${border} ${bg} p-5 shadow-card transition-shadow duration-200 hover:shadow-md`}
    >
      <p className="text-xs font-semibold uppercase tracking-wider text-text-muted">{title}</p>
      <p className="mt-2 tabular-nums lining-nums">
        <span className="text-2xl font-bold text-text">{value}</span>
        {unit && <span className="ml-1 text-base font-normal text-text-muted">{unit}</span>}
      </p>
      {subtitle && <p className="mt-0.5 text-sm text-text-muted">{subtitle}</p>}
      {deltaLabel && (
        <p className={`mt-2 text-xs font-medium ${deltaClass}`}>{deltaLabel}</p>
      )}
    </div>
  )
}
