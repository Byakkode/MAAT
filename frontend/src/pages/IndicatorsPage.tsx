import { type ChangeEvent, useEffect, useRef, useState } from 'react'
import { Leaf, ShoppingCart, TrendingUp, Users } from 'lucide-react'
import { ApiError } from '../api/authApi'
import * as indicatorsApi from '../api/indicatorsApi'
import type { RseIndicators } from '../api/indicatorsApi'
import { EMPTY_INDICATORS } from '../api/indicatorsApi'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'

// ─── Configuration des métriques ─────────────────────────────────────────────

interface MetricConfig {
  key: keyof RseIndicators
  label: string
  unit: string
  min?: number
  max?: number
  step?: number
  isInteger?: boolean
}

const DOMAINS: {
  id: string
  label: string
  icon: typeof Leaf
  color: string
  bgColor: string
  metrics: MetricConfig[]
}[] = [
  {
    id: 'env',
    label: 'Environnement',
    icon: Leaf,
    color: '#16a34a',
    bgColor: '#dcfce7',
    metrics: [
      { key: 'co2EmissionsTons', label: 'Émissions CO₂', unit: 'tCO₂eq/an', min: 0, step: 0.1 },
      { key: 'energyConsumptionKwh', label: 'Consommation énergie', unit: 'kWh/an', min: 0, step: 100 },
      { key: 'renewableEnergyPct', label: 'Part énergie renouvelable', unit: '%', min: 0, max: 100, step: 0.1 },
      { key: 'waterConsumptionM3', label: 'Consommation eau', unit: 'm³/an', min: 0, step: 1 },
      { key: 'wasteTons', label: 'Déchets produits', unit: 't/an', min: 0, step: 0.1 },
      { key: 'recyclingRatePct', label: 'Taux de recyclage', unit: '%', min: 0, max: 100, step: 0.1 },
    ],
  },
  {
    id: 'social',
    label: 'Social',
    icon: Users,
    color: '#1d4ed8',
    bgColor: '#dbeafe',
    metrics: [
      { key: 'employeeCountFte', label: 'Effectif', unit: 'ETP', min: 0, step: 0.5 },
      { key: 'turnoverRatePct', label: 'Taux de turnover', unit: '%', min: 0, max: 100, step: 0.1 },
      { key: 'trainingHoursPerEmployee', label: 'Formation / salarié', unit: 'h/an', min: 0, step: 0.5 },
      { key: 'workAccidentRate', label: "Taux d'accidents du travail", unit: '‰', min: 0, step: 0.01 },
      { key: 'genderEqualityIndex', label: 'Index égalité F/H', unit: '/100', min: 0, max: 100, step: 1 },
      { key: 'permanentContractPct', label: 'Part CDI', unit: '%', min: 0, max: 100, step: 0.1 },
    ],
  },
  {
    id: 'achats',
    label: 'Achats responsables',
    icon: ShoppingCart,
    color: '#7c3aed',
    bgColor: '#f3e8ff',
    metrics: [
      { key: 'localSuppliersPct', label: 'Fournisseurs locaux (< 100 km)', unit: '%', min: 0, max: 100, step: 0.1 },
      { key: 'rseAssessedSuppliersPct', label: 'Fournisseurs évalués RSE', unit: '%', min: 0, max: 100, step: 0.1 },
      { key: 'activeSuppliersCount', label: 'Nombre de fournisseurs actifs', unit: 'fournisseurs', min: 0, step: 1, isInteger: true },
    ],
  },
  {
    id: 'eco',
    label: 'Économique',
    icon: TrendingUp,
    color: '#a16207',
    bgColor: '#fef9c3',
    metrics: [
      { key: 'revenueEur', label: "Chiffre d'affaires", unit: '€', min: 0, step: 1000 },
      { key: 'rseInvestmentEur', label: 'Investissements RSE', unit: '€', min: 0, step: 100 },
      { key: 'exportRevenuePct', label: 'Part CA export', unit: '%', min: 0, max: 100, step: 0.1 },
    ],
  },
]

// ─── Utilitaires ──────────────────────────────────────────────────────────────

function formatDelta(current: number | null, previous: number | null): string | null {
  if (current === null || previous === null || previous === 0) return null
  const pct = ((current - previous) / Math.abs(previous)) * 100
  const sign = pct > 0 ? '+' : ''
  return `${sign}${pct.toFixed(1)}%`
}

const CURRENT_YEAR = new Date().getFullYear()
const YEAR_OPTIONS = Array.from({ length: 6 }, (_, i) => CURRENT_YEAR - i)

// ─── Composant ligne métrique ──────────────────────────────────────────────────

function MetricRow({
  metric,
  value,
  prevValue,
  onChange,
}: {
  metric: MetricConfig
  value: number | null
  prevValue: number | null
  onChange: (key: keyof RseIndicators, val: number | null) => void
}) {
  const delta = formatDelta(value, prevValue)

  function handleChange(e: ChangeEvent<HTMLInputElement>) {
    const raw = e.target.value
    if (raw === '' || raw === '-') {
      onChange(metric.key, null)
      return
    }
    const num = metric.isInteger ? parseInt(raw, 10) : parseFloat(raw)
    onChange(metric.key, isNaN(num) ? null : num)
  }

  return (
    <div className="flex items-center gap-3 border-b border-border py-3 last:border-0 last:pb-0 first:pt-0">
      <label
        htmlFor={`metric-${metric.key}`}
        className="flex-1 text-[13px] text-text-muted"
      >
        {metric.label}
      </label>

      <div className="flex items-center gap-2">
        {delta && (
          <span className="text-[11px] tabular-nums text-text-muted/60">{delta} vs N-1</span>
        )}
        <div className="flex items-center gap-1.5">
          <input
            id={`metric-${metric.key}`}
            type="number"
            min={metric.min}
            max={metric.max}
            step={metric.step ?? 'any'}
            value={value ?? ''}
            onChange={handleChange}
            placeholder=""
            className="w-28 rounded-lg border border-border bg-white px-2.5 py-1.5 text-right text-[13px] tabular-nums text-text placeholder:text-text-muted/40 transition-colors focus:border-blue-maat focus:outline-none focus:ring-1 focus:ring-blue-maat/20"
          />
          <span className="w-16 shrink-0 text-[11.5px] text-text-muted/60">{metric.unit}</span>
        </div>
      </div>
    </div>
  )
}

// ─── Page principale ──────────────────────────────────────────────────────────

type SaveStatus = 'idle' | 'dirty' | 'saving' | 'saved' | 'error'

export function IndicatorsPage() {
  const [year, setYear] = useState(CURRENT_YEAR)
  const [indicators, setIndicators] = useState<RseIndicators>(EMPTY_INDICATORS)
  const [prevIndicators, setPrevIndicators] = useState<RseIndicators | null>(null)
  const [loadStatus, setLoadStatus] = useState<'loading' | 'ready' | 'error'>('loading')
  const [saveStatus, setSaveStatus] = useState<SaveStatus>('idle')
  const [saveError, setSaveError] = useState<string | null>(null)
  const savedTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoadStatus('loading')
    setSaveStatus('idle')

    async function load() {
      const [current, previous] = await Promise.all([
        indicatorsApi.getIndicators(year),
        indicatorsApi.getIndicators(year - 1),
      ])
      if (cancelled) return
      setIndicators(current ?? EMPTY_INDICATORS)
      setPrevIndicators(previous)
      setLoadStatus('ready')
    }

    load().catch(() => {
      if (!cancelled) setLoadStatus('error')
    })

    return () => { cancelled = true }
  }, [year])

  function handleChange(key: keyof RseIndicators, value: number | null) {
    setIndicators((prev) => ({ ...prev, [key]: value }))
    setSaveStatus('dirty')
  }

  async function handleSave() {
    setSaveStatus('saving')
    setSaveError(null)
    try {
      const saved = await indicatorsApi.upsertIndicators(year, indicators)
      setIndicators(saved)
      setSaveStatus('saved')
      if (savedTimerRef.current) clearTimeout(savedTimerRef.current)
      savedTimerRef.current = setTimeout(() => setSaveStatus('idle'), 3000)
    } catch (err) {
      setSaveError(err instanceof ApiError ? err.message : 'Erreur lors de la sauvegarde.')
      setSaveStatus('error')
    }
  }

  const filledCount = Object.values(indicators).filter((v) => v !== null).length

  return (
    <div className="flex flex-col gap-5">
      {/* En-tête */}
      <div className="flex flex-wrap items-start justify-between gap-4">
        <PageHeader
          title="Indicateurs RSE"
          subtitle="Données chiffrées annuelles · tous les champs sont optionnels"
        />

        <div className="flex items-center gap-3">
          <select
            value={year}
            onChange={(e) => setYear(Number(e.target.value))}
            aria-label="Sélectionner l'année"
            className="rounded-xl border border-border bg-white px-3 py-2 text-[13px] text-text transition-colors focus:border-blue-maat focus:outline-none focus:ring-1 focus:ring-blue-maat/20"
          >
            {YEAR_OPTIONS.map((y) => (
              <option key={y} value={y}>{y}</option>
            ))}
          </select>

          <button
            type="button"
            onClick={() => void handleSave()}
            disabled={saveStatus !== 'dirty'}
            className="rounded-xl bg-blue-maat px-4 py-2 text-[13.5px] font-semibold text-white shadow-button-primary transition-opacity disabled:cursor-not-allowed disabled:opacity-40"
          >
            {saveStatus === 'saving'
              ? 'Enregistrement…'
              : saveStatus === 'saved'
                ? 'Enregistré ✓'
                : 'Enregistrer'}
          </button>
        </div>
      </div>

      {saveStatus === 'error' && saveError && (
        <p role="alert" className="text-[13px] text-red">{saveError}</p>
      )}

      {/* Bandeau de progression */}
      {loadStatus === 'ready' && filledCount > 0 && (
        <div className="flex items-center gap-2 rounded-xl bg-blue-maat/5 px-4 py-2.5">
          <span className="text-[13px] text-blue-maat">
            {filledCount} indicateur{filledCount > 1 ? 's' : ''} renseigné{filledCount > 1 ? 's' : ''} pour {year}
          </span>
          {prevIndicators && (
            <span className="text-[12px] text-text-muted">
              · comparaison avec {year - 1} disponible
            </span>
          )}
        </div>
      )}

      {loadStatus === 'loading' && (
        <Card>
          <div className="flex items-center justify-center py-10">
            <span className="text-[13px] text-text-muted">Chargement…</span>
          </div>
        </Card>
      )}

      {loadStatus === 'error' && (
        <Card>
          <p role="alert" className="text-[13px] text-red">
            Impossible de charger les indicateurs.
          </p>
        </Card>
      )}

      {loadStatus === 'ready' && (
        <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
          {DOMAINS.map((domain) => {
            const Icon = domain.icon
            const filled = domain.metrics.filter((m) => indicators[m.key] !== null).length

            return (
              <Card key={domain.id} as="section" aria-labelledby={`domain-${domain.id}`}>
                <div className="mb-4 flex items-center gap-3">
                  <div
                    className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg"
                    style={{ backgroundColor: domain.bgColor }}
                    aria-hidden="true"
                  >
                    <Icon size={16} style={{ color: domain.color }} strokeWidth={1.75} />
                  </div>
                  <h2 id={`domain-${domain.id}`} className="text-[14px] font-semibold text-text">
                    {domain.label}
                  </h2>
                  {filled > 0 && (
                    <span className="ml-auto text-[11px] text-text-muted">
                      {filled}/{domain.metrics.length}
                    </span>
                  )}
                </div>

                <div>
                  {domain.metrics.map((metric) => (
                    <MetricRow
                      key={metric.key}
                      metric={metric}
                      value={indicators[metric.key] as number | null}
                      prevValue={prevIndicators ? (prevIndicators[metric.key] as number | null) : null}
                      onChange={handleChange}
                    />
                  ))}
                </div>
              </Card>
            )
          })}
        </div>
      )}
    </div>
  )
}
