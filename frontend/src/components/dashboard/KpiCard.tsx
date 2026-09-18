import { Card } from '../ui/Card'

interface KpiCardProps {
  title: string
  value: string | number
  /** Affiché dans un span séparé après la valeur — permet aux tests de cibler la valeur seule. */
  unit?: string
  subtitle?: string
  /* Variation vs diagnostic précédent — positif = vert, négatif = rouge, 0 = muted */
  delta?: number | null
}

export function KpiCard({ title, value, unit, subtitle, delta }: KpiCardProps) {
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

  return (
    <Card>
      <p className="text-xs font-medium uppercase tracking-wide text-text-muted">{title}</p>
      <p className="mt-2 tabular-nums lining-nums">
        <span className="text-2xl font-bold text-text">{value}</span>
        {unit && <span className="ml-1 text-base font-normal text-text-muted">{unit}</span>}
      </p>
      {subtitle && <p className="mt-0.5 text-sm text-text-muted">{subtitle}</p>}
      {deltaLabel && (
        <p className={`mt-2 text-xs font-medium ${deltaClass}`}>{deltaLabel}</p>
      )}
    </Card>
  )
}
