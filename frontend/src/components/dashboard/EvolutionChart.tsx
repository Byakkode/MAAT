import { memo, useMemo } from 'react'
import { CartesianGrid, Line, LineChart, ResponsiveContainer, XAxis, YAxis } from 'recharts'
import type { DiagnosticHistoryPoint } from '../../types/dashboard'
import { Card } from '../ui/Card'

interface EvolutionChartProps {
  history: DiagnosticHistoryPoint[]
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })
}

function formatDelta(delta: number): string {
  const rounded = Math.round(delta)
  return rounded > 0 ? `+${rounded}` : `${rounded}`
}

// docs/specs/dashboard.md, section 4. Mémoïsé pour la même raison que DomainRadarChart
// (section 8) : Recharts recalcule sa géométrie à chaque rendu du parent.
function EvolutionChartComponent({ history }: EvolutionChartProps) {
  const data = useMemo(
    () => history.map((point) => ({ ...point, dateLabel: formatDate(point.completedAt) })),
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
  const deltaClass = delta > 0 ? 'text-green-maat-text' : delta < 0 ? 'text-red' : 'text-text-muted'

  const description = `Historique du score global, échelle de 0 à 100, du ${data[0]!.dateLabel} au ${latest.dateLabel}. ${data
    .map((point) => `${point.dateLabel} : ${Math.round(point.globalScore)} sur 100`)
    .join(', ')}.`

  return (
    <Card as="section">
      <div className="mb-3 flex items-start justify-between gap-4">
        <h2 className="text-base font-semibold text-text">Évolution du score</h2>
        <p className="shrink-0 text-sm tabular-nums lining-nums">
          <span className={`font-semibold ${deltaClass}`}>{formatDelta(delta)} pts</span>
          <span className="text-text-muted"> vs {previous.dateLabel}</span>
        </p>
      </div>

      <div role="img" aria-label={description} className="h-48 w-full">
        <div aria-hidden="true" className="h-full w-full">
          <ResponsiveContainer width="100%" height="100%">
            {/* accessibilityLayer désactivé : même raison que DomainRadarChart. */}
            <LineChart data={data} margin={{ top: 4, right: 4, bottom: 0, left: -20 }} accessibilityLayer={false}>
              <CartesianGrid stroke="var(--color-border)" strokeDasharray="3 3" vertical={false} />
              <XAxis
                dataKey="dateLabel"
                tick={{ fill: 'var(--color-text-muted)', fontSize: 10 }}
                axisLine={false}
                tickLine={false}
              />
              {/* Échelle fixe 0-100, jamais adaptée aux données (même règle que le radar). */}
              <YAxis
                domain={[0, 100]}
                tick={{ fill: 'var(--color-text-muted)', fontSize: 10 }}
                axisLine={false}
                tickLine={false}
              />
              <Line
                type="monotone"
                dataKey="globalScore"
                stroke="var(--color-blue-maat)"
                strokeWidth={2}
                dot={{ fill: 'var(--color-blue-maat)', r: 4, strokeWidth: 0 }}
                activeDot={{ r: 5, stroke: 'white', strokeWidth: 2 }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>
    </Card>
  )
}

export const EvolutionChart = memo(EvolutionChartComponent)
