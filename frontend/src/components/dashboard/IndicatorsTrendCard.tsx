import { memo, useEffect, useState } from 'react'
import { Leaf, ShoppingCart, TrendingUp, Users } from 'lucide-react'
import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis } from 'recharts'
import { Link } from 'react-router-dom'
import * as indicatorsApi from '../../api/indicatorsApi'
import type { RseIndicators } from '../../api/indicatorsApi'
import { Card } from '../ui/Card'

// ─── Config ───────────────────────────────────────────────────────────────────

interface MetricConfig {
  key: keyof RseIndicators
  label: string
  unit: string
}

const TABS = ['Environnement', 'Social', 'Achats', 'Économique'] as const
type TabId = typeof TABS[number]

const TAB_CONFIG: Record<TabId, { Icon: typeof Leaf; color: string; metrics: MetricConfig[] }> = {
  Environnement: {
    Icon: Leaf,
    color: '#29CC6A',
    metrics: [
      { key: 'co2EmissionsTons', label: 'Émissions CO₂', unit: 'tCO₂eq' },
      { key: 'energyConsumptionKwh', label: 'Énergie', unit: 'kWh' },
      { key: 'renewableEnergyPct', label: 'Énergie renouvelable', unit: '%' },
      { key: 'waterConsumptionM3', label: 'Eau', unit: 'm³' },
      { key: 'wasteTons', label: 'Déchets', unit: 't' },
      { key: 'recyclingRatePct', label: 'Recyclage', unit: '%' },
    ],
  },
  Social: {
    Icon: Users,
    color: '#1E88E5',
    metrics: [
      { key: 'employeeCountFte', label: 'Effectif', unit: 'ETP' },
      { key: 'turnoverRatePct', label: 'Turnover', unit: '%' },
      { key: 'trainingHoursPerEmployee', label: 'Formation / salarié', unit: 'h/an' },
      { key: 'workAccidentRate', label: 'Accidents du travail', unit: '‰' },
      { key: 'genderEqualityIndex', label: 'Égalité F/H', unit: '/100' },
      { key: 'permanentContractPct', label: 'Part CDI', unit: '%' },
    ],
  },
  Achats: {
    Icon: ShoppingCart,
    color: '#7E57C2',
    metrics: [
      { key: 'localSuppliersPct', label: 'Fournisseurs locaux', unit: '%' },
      { key: 'rseAssessedSuppliersPct', label: 'Évalués RSE', unit: '%' },
      { key: 'activeSuppliersCount', label: 'Nb fournisseurs', unit: '' },
    ],
  },
  Économique: {
    Icon: TrendingUp,
    color: '#FFB74D',
    metrics: [
      { key: 'revenueEur', label: "Chiffre d'affaires", unit: '€' },
      { key: 'rseInvestmentEur', label: 'Investissements RSE', unit: '€' },
      { key: 'exportRevenuePct', label: 'Part CA export', unit: '%' },
    ],
  },
}

// ─── Utilitaires ──────────────────────────────────────────────────────────────

function formatValue(value: number, unit: string): string {
  if (unit === '€') {
    return new Intl.NumberFormat('fr-FR', { notation: 'compact', maximumFractionDigits: 1 }).format(value)
  }
  if (value >= 10_000) {
    return new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 0 }).format(value)
  }
  const isWholeNumber = value === Math.floor(value)
  return new Intl.NumberFormat('fr-FR', { maximumFractionDigits: isWholeNumber ? 0 : 1 }).format(value)
}

interface YearPoint {
  year: number
  value: number
}

// ─── Sous-composants ──────────────────────────────────────────────────────────

function DeltaBadge({ current, previous }: { current: number; previous: number }) {
  if (previous === 0) return null
  const pct = ((current - previous) / Math.abs(previous)) * 100
  if (Math.abs(pct) < 0.05) return null
  const isUp = pct > 0
  return (
    <span className={`shrink-0 text-[10.5px] font-semibold tabular-nums ${isUp ? 'text-green-maat-text' : 'text-red'}`}>
      {isUp ? '+' : ''}{pct.toFixed(1)}%
    </span>
  )
}

interface TooltipPayloadItem {
  value: number
  payload: YearPoint
}

function MiniTooltipContent({ active, payload, unit }: { active?: boolean; payload?: TooltipPayloadItem[]; unit: string }) {
  if (!active || !payload?.length) return null
  const item = payload[0]!
  return (
    <div className="rounded-lg border border-border bg-white px-2.5 py-2 text-[11px] shadow-popover">
      <p className="font-semibold text-text">{formatValue(item.value, unit)}{unit ? ` ${unit}` : ''}</p>
      <p className="text-text-muted">{item.payload.year}</p>
    </div>
  )
}

function MetricTrendChart({ metric, data, color }: { metric: MetricConfig; data: YearPoint[]; color: string }) {
  if (data.length === 0) return null

  const latest = data[data.length - 1]!
  const previous = data.length >= 2 ? data[data.length - 2]! : null

  // Recharts Tooltip attend une factory qui reçoit les props — on ferme sur `metric.unit`.
  const TooltipContent = ({ active, payload }: { active?: boolean; payload?: TooltipPayloadItem[] }) => (
    <MiniTooltipContent active={active} payload={payload} unit={metric.unit} />
  )

  return (
    <div className="flex flex-col rounded-xl border border-border bg-white p-3.5">
      <div className="mb-1 flex items-start justify-between gap-1">
        <p className="text-[11px] leading-snug text-text-muted">{metric.label}</p>
        {previous && (
          <DeltaBadge current={latest.value} previous={previous.value} />
        )}
      </div>

      <div className="flex items-baseline gap-0.5">
        <span className="text-[1.25rem] font-bold leading-none tabular-nums lining-nums text-text">
          {formatValue(latest.value, metric.unit)}
        </span>
        {metric.unit && (
          <span className="ml-0.5 text-[10.5px] text-text-muted">{metric.unit}</span>
        )}
      </div>

      {data.length >= 2 ? (
        <div className="mt-2 h-[56px]" aria-hidden="true">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={data} margin={{ top: 4, right: 4, bottom: 0, left: 0 }}>
              <XAxis dataKey="year" hide />
              <Tooltip content={<TooltipContent />} cursor={false} />
              <Line
                type="monotone"
                dataKey="value"
                stroke={color}
                strokeWidth={2}
                dot={{ r: 2.5, fill: color, strokeWidth: 0 }}
                activeDot={{ r: 3.5, stroke: 'white', strokeWidth: 1.5, fill: color }}
                isAnimationActive={false}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      ) : (
        <p className="mt-1.5 text-[10.5px] text-text-muted/60">Première année renseignée</p>
      )}
    </div>
  )
}

const MetricTrendChartMemo = memo(MetricTrendChart)

function TabButton({ id, active, onClick }: { id: TabId; active: boolean; onClick: () => void }) {
  const { Icon, color } = TAB_CONFIG[id]
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-[12.5px] font-medium transition-all ${
        active ? 'bg-white shadow-sm text-text' : 'text-text-muted hover:text-text'
      }`}
    >
      <Icon size={13} style={{ color: active ? color : undefined }} strokeWidth={1.75} aria-hidden />
      {id}
    </button>
  )
}

// ─── Composant principal ──────────────────────────────────────────────────────

interface YearlyData {
  year: number
  indicators: RseIndicators
}

export function IndicatorsTrendCard() {
  const [activeTab, setActiveTab] = useState<TabId>('Environnement')
  const [loadStatus, setLoadStatus] = useState<'loading' | 'ready' | 'empty' | 'error'>('loading')
  const [yearlyData, setYearlyData] = useState<YearlyData[]>([])

  useEffect(() => {
    let cancelled = false

    async function load() {
      try {
        const years = await indicatorsApi.getYearsWithData()
        if (!years.length) {
          if (!cancelled) setLoadStatus('empty')
          return
        }
        const sorted = [...years].sort((a, b) => a - b)
        const allIndicators = await Promise.all(sorted.map((y) => indicatorsApi.getIndicators(y)))
        if (cancelled) return

        const data: YearlyData[] = sorted
          .map((year, i) => ({ year, indicators: allIndicators[i] }))
          .filter((d): d is YearlyData => d.indicators !== null)

        setYearlyData(data)
        setLoadStatus(data.length ? 'ready' : 'empty')
      } catch {
        if (!cancelled) setLoadStatus('error')
      }
    }

    void load()
    return () => { cancelled = true }
  }, [])

  const { metrics, color } = TAB_CONFIG[activeTab]

  const metricPoints = metrics
    .map((m) => ({
      metric: m,
      data: yearlyData
        .map((yd) => ({ year: yd.year, value: yd.indicators[m.key] as number | null }))
        .filter((d): d is YearPoint => d.value !== null),
    }))
    .filter((mp) => mp.data.length > 0)

  return (
    <Card as="section" aria-labelledby="indicators-trend-heading">
      {/* En-tête */}
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 id="indicators-trend-heading" className="text-base font-semibold text-text">
            Évolution des indicateurs
          </h2>
          {loadStatus === 'ready' && yearlyData.length > 0 && (
            <p className="mt-0.5 text-[12px] text-text-muted">
              {yearlyData.length} année{yearlyData.length > 1 ? 's' : ''} renseignée{yearlyData.length > 1 ? 's' : ''}
              {' · '}{yearlyData.map((d) => d.year).join(', ')}
            </p>
          )}
        </div>
        <Link to="/indicateurs" className="text-[12px] font-medium text-blue-maat-text hover:underline">
          Gérer les indicateurs →
        </Link>
      </div>

      {/* États de chargement */}
      {loadStatus === 'loading' && (
        <div className="flex items-center justify-center py-10">
          <span className="text-[13px] text-text-muted">Chargement…</span>
        </div>
      )}

      {loadStatus === 'error' && (
        <p className="text-[13px] text-red">Impossible de charger les indicateurs.</p>
      )}

      {loadStatus === 'empty' && (
        <div className="flex flex-col items-center py-10 text-center">
          <p className="text-[13px] text-text-muted">
            Renseignez vos indicateurs RSE pour voir leur évolution ici.
          </p>
          <Link to="/indicateurs" className="mt-3 text-[13px] font-medium text-blue-maat-text hover:underline">
            Ajouter des indicateurs →
          </Link>
        </div>
      )}

      {loadStatus === 'ready' && (
        <>
          {/* Onglets */}
          <div className="mb-4 flex flex-wrap gap-1 rounded-xl bg-bg p-1">
            {TABS.map((tab) => (
              <TabButton key={tab} id={tab} active={activeTab === tab} onClick={() => setActiveTab(tab)} />
            ))}
          </div>

          {/* Grille de sparklines */}
          {metricPoints.length === 0 ? (
            <div className="flex flex-col items-center py-6 text-center">
              <p className="text-[13px] text-text-muted">
                Aucun indicateur renseigné pour ce domaine.
              </p>
              <Link to="/indicateurs" className="mt-2 text-[13px] font-medium text-blue-maat-text hover:underline">
                Renseigner des données →
              </Link>
            </div>
          ) : (
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
              {metricPoints.map(({ metric, data }) => (
                <MetricTrendChartMemo key={metric.key} metric={metric} data={data} color={color} />
              ))}
            </div>
          )}
        </>
      )}
    </Card>
  )
}
