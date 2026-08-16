import { memo, useMemo } from 'react'
import { CartesianGrid, Line, LineChart, ResponsiveContainer, XAxis, YAxis } from 'recharts'
import type { DiagnosticHistoryPoint } from '../../types/dashboard'

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
      <div className="rounded-card border border-border bg-white p-5 shadow-card">
        <h3 className="mb-2 text-base font-semibold text-text">Historique d&apos;évolution</h3>
        <p className="text-sm text-text-muted">
          Vous pourrez renouveler votre diagnostic dans quelques mois pour voir apparaître ici votre courbe de
          progression.
        </p>
      </div>
    )
  }

  const latest = data[data.length - 1]!
  const previous = data[data.length - 2]!
  const delta = latest.deltaFromPrevious ?? latest.globalScore - previous.globalScore

  const description = `Historique du score global, échelle de 0 à 100, du ${data[0]!.dateLabel} au ${latest.dateLabel}. ${data
    .map((point) => `${point.dateLabel} : ${Math.round(point.globalScore)} sur 100`)
    .join(', ')}.`

  return (
    <div className="rounded-card border border-border bg-white p-5 shadow-card">
      <h3 className="mb-1 text-base font-semibold text-text">Historique d&apos;évolution</h3>
      <p className="mb-2 text-sm text-text-muted tabular-nums lining-nums">
        {formatDelta(delta)} points depuis le {previous.dateLabel}
      </p>

      <div role="img" aria-label={description} className="h-56 w-full">
        <div aria-hidden="true" className="h-full w-full">
          <ResponsiveContainer width="100%" height="100%">
            {/* accessibilityLayer désactivé : même raison que DomainRadarChart. */}
            <LineChart data={data} accessibilityLayer={false}>
              <CartesianGrid stroke="var(--color-border)" strokeDasharray="3 3" />
              <XAxis dataKey="dateLabel" tick={{ fill: 'var(--color-text-muted)', fontSize: 11 }} />
              {/* Échelle fixe 0-100, jamais adaptée aux données (même règle que le radar). */}
              <YAxis domain={[0, 100]} tick={{ fill: 'var(--color-text-muted)', fontSize: 11 }} />
              <Line type="monotone" dataKey="globalScore" stroke="var(--color-blue-maat)" strokeWidth={2} dot />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  )
}

export const EvolutionChart = memo(EvolutionChartComponent)
