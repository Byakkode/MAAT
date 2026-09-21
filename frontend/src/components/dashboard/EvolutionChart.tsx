import { memo, useMemo } from 'react'
import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { DiagnosticHistoryPoint } from '../../types/dashboard'
import { Card } from '../ui/Card'

interface EvolutionChartProps {
  history: DiagnosticHistoryPoint[]
}

function formatDateFull(iso: string): string {
  return new Date(iso).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })
}

function formatDateShort(iso: string): string {
  return new Date(iso).toLocaleDateString('fr-FR', { day: 'numeric', month: 'short' })
}

interface EvolutionTooltipProps {
  active?: boolean
  payload?: ReadonlyArray<{ payload: DiagnosticHistoryPoint }>
}

function EvolutionTooltipContent({ active, payload }: EvolutionTooltipProps) {
  if (!active || !payload?.length) return null
  const point = payload[0]!.payload
  return (
    <div className="rounded-xl border border-border bg-white px-3 py-2.5 text-sm shadow-popover tabular-nums lining-nums">
      <p className="text-[1.125rem] font-bold leading-none text-text">{Math.round(point.globalScore)}<span className="ml-1 text-sm font-medium text-text-muted">/ 100</span></p>
      <p className="mt-1 text-[12px] text-text-muted">{formatDateFull(point.completedAt)}</p>
    </div>
  )
}

// docs/specs/dashboard.md, section 4. Mémoïsé pour la même raison que DomainRadarChart
// (section 8) : Recharts recalcule sa géométrie à chaque rendu du parent.
function EvolutionChartComponent({ history }: EvolutionChartProps) {
  const data = useMemo(
    () => history.map((point) => ({ ...point, dateLabel: formatDateShort(point.completedAt) })),
    [history],
  )

  // Un seul point : rien à comparer, la courbe n'apporte rien tant qu'un second diagnostic
  // n'a pas été réalisé (cas 17).
  if (history.length < 2) {
    return (
      <Card as="section">
        <h2 className="mb-1 text-base font-semibold text-text">Évolution du score</h2>
        <p className="text-sm text-text-muted">
          Vous pourrez renouveler votre diagnostic dans quelques mois pour voir apparaître ici votre
          courbe de progression.
        </p>
      </Card>
    )
  }

  const latest = data[data.length - 1]!
  const previous = data[data.length - 2]!
  const delta = latest.deltaFromPrevious ?? latest.globalScore - previous.globalScore
  const isUp = delta > 0
  const pillClass = isUp
    ? 'bg-green-maat/10 text-green-maat-text'
    : delta < 0
      ? 'bg-red/10 text-red'
      : 'bg-border text-text-muted'

  const description = `Historique du score global, échelle de 0 à 100, du ${formatDateFull(data[0]!.completedAt)} au ${formatDateFull(latest.completedAt)}. ${data
    .map((point) => `${formatDateFull(point.completedAt)} : ${Math.round(point.globalScore)} sur 100`)
    .join(', ')}.`

  return (
    <Card as="section">
      <div className="mb-4 flex items-start justify-between gap-4">
        <div>
          <h2 className="text-base font-semibold text-text">Évolution du score</h2>
          <p className="mt-0.5 text-[12px] text-text-muted">
            vs {formatDateFull(previous.completedAt)}
          </p>
        </div>
        <span
          className={`mt-0.5 inline-flex shrink-0 items-center rounded-full px-2 py-0.5 text-[12px] font-semibold tabular-nums lining-nums ${pillClass}`}
        >
          {isUp ? '+' : ''}{Math.round(delta)} pts
        </span>
      </div>

      <div role="img" aria-label={description} className="h-52 w-full">
        <div aria-hidden="true" className="h-full w-full">
          <ResponsiveContainer width="100%" height="100%">
            {/* accessibilityLayer désactivé : même raison que DomainRadarChart.
                AreaChart remplace LineChart : la zone de gradient bleu sous la courbe
                renforce la lisibilité de la progression sans ajouter d'information. */}
            <AreaChart data={data} margin={{ top: 4, right: 4, bottom: 0, left: -20 }} accessibilityLayer={false}>
              <defs>
                <linearGradient id="evo-gradient" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#1565ff" stopOpacity={0.14} />
                  <stop offset="100%" stopColor="#1565ff" stopOpacity={0.02} />
                </linearGradient>
              </defs>
              <CartesianGrid stroke="var(--color-border)" strokeDasharray="3 3" vertical={false} />
              <XAxis
                dataKey="dateLabel"
                tick={{ fill: 'var(--color-text-muted)', fontSize: 11 }}
                axisLine={false}
                tickLine={false}
              />
              {/* Échelle fixe 0-100, jamais adaptée aux données (même règle que le radar). */}
              <YAxis
                domain={[0, 100]}
                tick={{ fill: 'var(--color-text-muted)', fontSize: 11 }}
                axisLine={false}
                tickLine={false}
              />
              <Tooltip
                content={<EvolutionTooltipContent />}
                cursor={{ stroke: 'var(--color-border)', strokeWidth: 1 }}
              />
              <Area
                type="monotone"
                dataKey="globalScore"
                stroke="var(--color-blue-maat)"
                strokeWidth={2}
                fill="url(#evo-gradient)"
                dot={{ fill: 'var(--color-blue-maat)', r: 4, strokeWidth: 0 }}
                activeDot={{ r: 5, stroke: 'white', strokeWidth: 2 }}
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </div>
    </Card>
  )
}

export const EvolutionChart = memo(EvolutionChartComponent)
