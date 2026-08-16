import { memo, useMemo } from 'react'
import {
  PolarAngleAxis,
  PolarGrid,
  PolarRadiusAxis,
  Radar,
  RadarChart,
  ResponsiveContainer,
  Tooltip,
  type DotItemDotProps,
} from 'recharts'
import { DOMAIN_COLORS } from '../../constants/domainColors'
import { DOMAIN_LABELS, DOMAIN_ORDER } from '../../types/questionnaire'
import type { DomainScore } from '../../types/dashboard'

interface DomainRadarChartProps {
  domainScores: DomainScore[]
}

interface RadarPoint {
  domain: (typeof DOMAIN_ORDER)[number]
  label: string
  score: number
  sectorWeight: number
  triggeredRecommendationCount: number
}

function formatPercent(weight: number): string {
  return `${Math.round(weight * 100)} %`
}

interface RadarTooltipContentProps {
  active?: boolean
  payload?: ReadonlyArray<{ payload: RadarPoint }>
}

// Recharts injecte active/payload à l'exécution (le composant "content" est cloné par
// Tooltip) : le type exporté TooltipProps les exclut volontairement (lus "depuis le
// contexte"), d'où cette interface locale plutôt qu'un import Recharts inexact ici.
function RadarTooltipContent({ active, payload }: RadarTooltipContentProps) {
  if (!active || !payload || payload.length === 0) {
    return null
  }
  const point = payload[0]!.payload

  return (
    <div className="rounded-card border border-border bg-white p-3 text-sm shadow-card tabular-nums lining-nums">
      <p className="font-medium text-text">{point.label}</p>
      <p className="text-text-muted">Score : {Math.round(point.score)} / 100</p>
      <p className="text-text-muted">Pondération sectorielle : {formatPercent(point.sectorWeight)}</p>
      <p className="text-text-muted">
        {point.triggeredRecommendationCount} recommandation{point.triggeredRecommendationCount > 1 ? 's' : ''} déclenchée
        {point.triggeredRecommendationCount > 1 ? 's' : ''}
      </p>
    </div>
  )
}

// docs/specs/dashboard.md, section 3. Mémoïsé : Recharts recalcule sa géométrie à chaque
// rendu du parent (section 8) ; le graphique ne se re-rend donc que si domainScores change
// réellement.
function DomainRadarChartComponent({ domainScores }: DomainRadarChartProps) {
  const data = useMemo<RadarPoint[]>(
    () =>
      DOMAIN_ORDER.map((domain) => {
        const found = domainScores.find((d) => d.domain === domain)
        return {
          domain,
          label: DOMAIN_LABELS[domain],
          score: found?.score ?? 0,
          sectorWeight: found?.sectorWeight ?? 0,
          triggeredRecommendationCount: found?.triggeredRecommendationCount ?? 0,
        }
      }),
    [domainScores],
  )

  // L'échelle (0 à 100) est énoncée en toutes lettres, jamais calculée depuis les données :
  // c'est ce qui garantit qu'elle ne varie jamais, y compris dans cette description.
  const description = useMemo(
    () =>
      `Radar des cinq domaines RSE, échelle de 0 à 100. ${data
        .map((d) => `${d.label} : ${Math.round(d.score)} sur 100`)
        .join(', ')}.`,
    [data],
  )

  return (
    <div>
      <h3 className="mb-2 text-base font-semibold text-text">Radar des cinq domaines</h3>
      <div role="img" aria-label={description} className="h-72 w-full">
        {/* aria-hidden : la structure SVG de Recharts n'est jamais exposée séparément à
            l'AT, qui reçoit déjà la description complète ci-dessus (voir DomainScoreTable
            pour l'équivalent tabulaire, exigé par la même section). */}
        <div aria-hidden="true" className="h-full w-full">
          <ResponsiveContainer width="100%" height="100%">
            {/* accessibilityLayer désactivé : Recharts y ajoute par défaut un focus clavier et
                des annonces ARIA propres, qui entreraient en conflit avec le rôle "img" et le
                tableau alternatif ci-dessus — un seul mécanisme d'accessibilité par graphique. */}
            <RadarChart data={data} accessibilityLayer={false}>
              <PolarGrid stroke="var(--color-border)" />
              <PolarAngleAxis dataKey="label" tick={{ fill: 'var(--color-text)', fontSize: 12 }} />
              {/* Échelle fixe 0-100, jamais adaptée aux données (section 3). */}
              <PolarRadiusAxis domain={[0, 100]} tick={false} axisLine={false} />
              <Radar
                dataKey="score"
                stroke="var(--color-blue-maat)"
                fill="var(--color-blue-maat)"
                fillOpacity={0.25}
                dot={(dotProps: DotItemDotProps) => {
                  const point = dotProps.payload as RadarPoint
                  return (
                    <circle
                      key={point.domain}
                      cx={dotProps.cx ?? 0}
                      cy={dotProps.cy ?? 0}
                      r={5}
                      fill={DOMAIN_COLORS[point.domain]}
                      stroke="var(--color-bg)"
                      strokeWidth={1}
                    />
                  )
                }}
              />
              <Tooltip content={<RadarTooltipContent />} />
            </RadarChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  )
}

export const DomainRadarChart = memo(DomainRadarChartComponent)
