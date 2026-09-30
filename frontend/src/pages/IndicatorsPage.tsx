import { type ChangeEvent, useCallback, useEffect, useRef, useState } from 'react'
import { Leaf, ShoppingCart, TrendingUp, Users } from 'lucide-react'
import { ApiError } from '../api/authApi'
import * as indicatorsApi from '../api/indicatorsApi'
import type { RseIndicators } from '../api/indicatorsApi'
import { EMPTY_INDICATORS } from '../api/indicatorsApi'
import * as vsmeApi from '../api/vsmeApi'
import type {
  Certification,
  CompanySite,
  CountryHeadcount,
  EmployeeCountUnit,
  Pollutant,
  PollutionMedium,
  ReportingBasis,
  Subsidiary,
  SustainabilityTopic,
  VsmeCompleteness,
  VsmeDisclosure,
  VsmeStatement,
} from '../api/vsmeApi'
import { EMPTY_STATEMENT } from '../api/vsmeApi'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { useEntitlements } from '../billing/entitlements'
import { UpgradeNotice } from '../components/billing/UpgradeNotice'
import { SitesEditor } from '../components/indicators/SitesEditor'
import {
  DisclosureCard,
  LIST_INPUT_CLASS,
  ListEditor,
  NumberField,
  SelectField,
  TextAreaField,
  TextField,
  YesNoField,
} from '../components/indicators/VsmeFields'
import {
  LEGAL_FORM_SUGGESTIONS,
  SUSTAINABILITY_TOPIC_LABELS,
  VSME_DISCLOSURE_LABELS,
  VSME_DISCLOSURES,
} from '../constants/vsme'

// docs/specs/norme-volontaire.md, section 5 : les informations B1 à B11 du module de base de la
// norme volontaire (ex-VSME), dans l'ordre de la norme, puis les compléments propres à MAAT.
// Mêmes libellés et mêmes unités que la section 04 du rapport PDF
// (backend/MAAT.Application/UseCases/ReportSustainabilityBuilder.cs, ReportIndicatorCatalog.cs).

// ─── Compléments (hors norme) ────────────────────────────────────────────────

interface MetricConfig {
  key: keyof RseIndicators
  label: string
  unit: string
  min?: number
  max?: number
  step?: number
  isInteger?: boolean
}

const COMPLEMENT_GROUPS: {
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
    metrics: [{ key: 'renewableEnergyPct', label: 'Part énergie renouvelable', unit: '%', min: 0, max: 100, step: 0.1 }],
  },
  {
    id: 'social',
    label: 'Social',
    icon: Users,
    color: '#1d4ed8',
    bgColor: '#dbeafe',
    metrics: [
      { key: 'turnoverRatePct', label: 'Taux de turnover', unit: '%', min: 0, max: 100, step: 0.1 },
      { key: 'workAccidentRate', label: 'Taux de fréquence des accidents', unit: '/ million h', min: 0, step: 0.01 },
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
      { key: 'rseInvestmentEur', label: 'Investissements RSE', unit: '€', min: 0, step: 100 },
      { key: 'exportRevenuePct', label: 'Part CA export', unit: '%', min: 0, max: 100, step: 0.1 },
    ],
  },
]

function formatDelta(current: number | null, previous: number | null): string | null {
  if (current === null || previous === null || previous === 0) return null
  const pct = ((current - previous) / Math.abs(previous)) * 100
  const sign = pct > 0 ? '+' : ''
  return `${sign}${pct.toFixed(1)}%`
}

const CURRENT_YEAR = new Date().getFullYear()
const YEAR_OPTIONS = Array.from({ length: 6 }, (_, i) => CURRENT_YEAR - i)

function MetricRow({
  metric,
  value,
  prevValue,
  onChange,
  readOnly,
}: {
  metric: MetricConfig
  value: number | null
  prevValue: number | null
  onChange: (key: keyof RseIndicators, val: number | null) => void
  readOnly: boolean
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
      <label htmlFor={`metric-${metric.key}`} className="flex-1 text-[13px] text-text-muted">
        {metric.label}
      </label>

      <div className="flex items-center gap-2">
        {delta && <span className="text-[11px] tabular-nums text-text-muted/60">{delta} vs N-1</span>}
        <div className="flex items-center gap-1.5">
          <input
            id={`metric-${metric.key}`}
            type="number"
            min={metric.min}
            max={metric.max}
            step={metric.step ?? 'any'}
            value={value ?? ''}
            onChange={handleChange}
            readOnly={readOnly}
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

const MEDIUM_LABELS: Record<PollutionMedium, string> = { Air: 'Air', Water: 'Eau', Soil: 'Sol' }

export function IndicatorsPage() {
  const [year, setYear] = useState(CURRENT_YEAR)
  // docs/specs/abonnement.md, section 8 : saisie ouverte dès Essential (norme-volontaire.md).
  // Les valeurs déjà saisies (offre précédente) restent consultables.
  const { canEditIndicators } = useEntitlements()
  const readOnly = !canEditIndicators
  const [indicators, setIndicators] = useState<RseIndicators>(EMPTY_INDICATORS)
  const [prevIndicators, setPrevIndicators] = useState<RseIndicators | null>(null)
  const [statement, setStatement] = useState<VsmeStatement>(EMPTY_STATEMENT)
  const [sites, setSites] = useState<CompanySite[]>([])
  const [completeness, setCompleteness] = useState<VsmeCompleteness | null>(null)
  const [loadStatus, setLoadStatus] = useState<'loading' | 'ready' | 'error'>('loading')
  const [saveStatus, setSaveStatus] = useState<SaveStatus>('idle')
  const [saveError, setSaveError] = useState<string | null>(null)
  const savedTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoadStatus('loading')
    setSaveStatus('idle')

    async function load() {
      const [current, previous, currentStatement, currentSites, currentCompleteness] = await Promise.all([
        indicatorsApi.getIndicators(year),
        indicatorsApi.getIndicators(year - 1),
        vsmeApi.getStatement(year),
        vsmeApi.listSites(),
        vsmeApi.getCompleteness(year),
      ])
      if (cancelled) return
      setIndicators({ ...EMPTY_INDICATORS, ...current })
      setPrevIndicators(previous)
      setStatement({ ...EMPTY_STATEMENT, ...currentStatement })
      setSites(currentSites)
      setCompleteness(currentCompleteness)
      setLoadStatus('ready')
    }

    load().catch(() => {
      if (!cancelled) setLoadStatus('error')
    })

    return () => {
      cancelled = true
    }
  }, [year])

  const refreshCompleteness = useCallback(async () => {
    setCompleteness(await vsmeApi.getCompleteness(year))
  }, [year])

  function setIndicator(key: keyof RseIndicators, value: number | null) {
    setIndicators((prev) => ({ ...prev, [key]: value }))
    setSaveStatus('dirty')
  }

  function setField<K extends keyof VsmeStatement>(key: K, value: VsmeStatement[K]) {
    setStatement((prev) => ({ ...prev, [key]: value }))
    setSaveStatus('dirty')
  }

  function handleSitesChange(next: CompanySite[]) {
    setSites(next)
    void refreshCompleteness()
  }

  async function handleSave() {
    setSaveStatus('saving')
    setSaveError(null)
    try {
      const [savedIndicators, savedStatement] = await Promise.all([
        indicatorsApi.upsertIndicators(year, indicators),
        vsmeApi.saveStatement(year, statement),
      ])
      setIndicators({ ...EMPTY_INDICATORS, ...savedIndicators })
      setStatement({ ...EMPTY_STATEMENT, ...savedStatement })
      await refreshCompleteness()
      setSaveStatus('saved')
      if (savedTimerRef.current) clearTimeout(savedTimerRef.current)
      savedTimerRef.current = setTimeout(() => setSaveStatus('idle'), 3000)
    } catch (err) {
      setSaveError(err instanceof ApiError ? err.message : 'Erreur lors de la sauvegarde.')
      setSaveStatus('error')
    }
  }

  const byCode = (code: VsmeDisclosure) => completeness?.disclosures.find((d) => d.disclosure === code)
  // §8 : données facultatives jusqu'à 10 salariés.
  const microHint = completeness?.isMicro ? 'Facultatif jusqu’à 10 salariés' : undefined
  const prev = (key: keyof RseIndicators) => (prevIndicators ? (prevIndicators[key] as number | null) : null)

  // Raccourci pour un champ chiffré de RseIndicators.
  const num = (key: keyof RseIndicators, label: string, unit: string, options: { hint?: string; integer?: boolean } = {}) => (
    <NumberField
      id={`ind-${key}`}
      label={label}
      unit={unit}
      value={indicators[key] as number | null}
      previous={prev(key)}
      onChange={(value) => setIndicator(key, value)}
      readOnly={readOnly}
      hint={options.hint}
      integer={options.integer}
    />
  )

  const yesNo = (key: keyof VsmeStatement, label: string, hint?: string) => (
    <YesNoField
      name={`st-${key}`}
      label={label}
      value={statement[key] as boolean | null}
      onChange={(value) => setField(key, value as never)}
      readOnly={readOnly}
      hint={hint}
    />
  )

  const saveButton = canEditIndicators && (
    <button
      type="button"
      onClick={() => void handleSave()}
      disabled={saveStatus !== 'dirty'}
      className="rounded-xl bg-blue-maat px-4 py-2 text-[13.5px] font-semibold text-white shadow-button-primary transition-opacity disabled:cursor-not-allowed disabled:opacity-40"
    >
      {saveStatus === 'saving' ? 'Enregistrement…' : saveStatus === 'saved' ? 'Enregistré ✓' : 'Enregistrer'}
    </button>
  )

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <PageHeader
          title="Indicateurs RSE"
          subtitle="Informations de durabilité de l’exercice, selon la norme volontaire européenne (ex-VSME), et compléments"
        />

        <div className="flex items-center gap-3">
          <select
            value={year}
            onChange={(e) => setYear(Number(e.target.value))}
            aria-label="Sélectionner l'année"
            className="rounded-xl border border-border bg-white px-3 py-2 text-[13px] text-text transition-colors focus:border-blue-maat focus:outline-none focus:ring-1 focus:ring-blue-maat/20"
          >
            {YEAR_OPTIONS.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
          {saveButton}
        </div>
      </div>

      {!canEditIndicators && (
        <UpgradeNotice requiredPlan="Essential" title="Préparez votre rapport selon la norme volontaire">
          Énergie, émissions, effectifs, sites : saisissez les informations B1 à B11 chaque année pour les retrouver dans votre
          rapport, prêt à transmettre à vos clients et à votre banque.
        </UpgradeNotice>
      )}

      {saveStatus === 'error' && saveError && (
        <p role="alert" className="text-[13px] text-red">
          {saveError}
        </p>
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

      {loadStatus === 'ready' && completeness && (
        <>
          <CompletenessBanner completeness={completeness} year={year} />

          <section aria-labelledby="vsme-heading" className="flex flex-col gap-4">
            <h2 id="vsme-heading" className="text-[16px] font-semibold text-text">
              Informations de durabilité (norme volontaire)
            </h2>

            <DisclosureCard code="B1" completeness={byCode('B1')}>
              <SelectField<ReportingBasis>
                id="st-reportingBasis"
                label="Base d’établissement du rapport"
                value={statement.reportingBasis}
                options={[
                  { value: 'Individual', label: 'Individuelle (l’entreprise seule)' },
                  { value: 'Consolidated', label: 'Consolidée (avec les filiales)' },
                ]}
                onChange={(value) => setField('reportingBasis', value)}
                readOnly={readOnly}
              />
              {statement.reportingBasis === 'Consolidated' && (
                <ListEditor<Subsidiary>
                  label="Filiales incluses dans le rapport"
                  items={statement.subsidiaries}
                  empty={{ name: '', registeredAddress: '' }}
                  onChange={(items) => setField('subsidiaries', items)}
                  readOnly={readOnly}
                  addLabel="Ajouter une filiale"
                  renderItem={(item, update, index) => (
                    <>
                      <input aria-label={`Nom de la filiale ${index + 1}`} value={item.name} readOnly={readOnly} onChange={(e) => update({ ...item, name: e.target.value })} className={LIST_INPUT_CLASS} placeholder="Nom" />
                      <input aria-label={`Adresse du siège de la filiale ${index + 1}`} value={item.registeredAddress} readOnly={readOnly} onChange={(e) => update({ ...item, registeredAddress: e.target.value })} className={LIST_INPUT_CLASS} placeholder="Adresse du siège" />
                    </>
                  )}
                />
              )}
              <TextField id="st-legalForm" label="Forme juridique" value={statement.legalForm} onChange={(v) => setField('legalForm', v)} readOnly={readOnly} list="legal-forms" />
              <datalist id="legal-forms">
                {LEGAL_FORM_SUGGESTIONS.map((form) => (
                  <option key={form} value={form} />
                ))}
              </datalist>
              <NumberField id="st-totalAssetsEur" label="Total du bilan" unit="€" value={statement.totalAssetsEur} onChange={(v) => setField('totalAssetsEur', v)} readOnly={readOnly} />
              {num('revenueEur', 'Chiffre d’affaires', '€')}
              {num('employeeCountFte', 'Effectif en ETP', 'ETP', { hint: 'Utilisé si la répartition par contrat (B8) n’est pas renseignée' })}
              <TextField id="st-primaryCountry" label="Pays principal d’activité" value={statement.primaryCountry} onChange={(v) => setField('primaryCountry', v)} readOnly={readOnly} placeholder="France" />
              <p className="border-b border-border py-2.5 text-[12.5px] text-text-muted">
                Code NACE : déduit de votre code NAF (ses quatre premiers caractères).
              </p>
              <SitesEditor sites={sites} onChange={handleSitesChange} readOnly={readOnly} />
              <ListEditor<Certification>
                label="Certifications et labels de durabilité (facultatif)"
                items={statement.certifications}
                empty={{ name: '', issuer: null, obtainedOn: null, rating: null }}
                onChange={(items) => setField('certifications', items)}
                readOnly={readOnly}
                addLabel="Ajouter un label"
                renderItem={(item, update, index) => (
                  <>
                    <input aria-label={`Label ${index + 1}`} value={item.name} readOnly={readOnly} onChange={(e) => update({ ...item, name: e.target.value })} className={LIST_INPUT_CLASS} placeholder="Label ou certification" />
                    <input aria-label={`Organisme du label ${index + 1}`} value={item.issuer ?? ''} readOnly={readOnly} onChange={(e) => update({ ...item, issuer: e.target.value || null })} className={LIST_INPUT_CLASS} placeholder="Organisme" />
                    <input aria-label={`Date du label ${index + 1}`} type="date" value={item.obtainedOn ?? ''} readOnly={readOnly} onChange={(e) => update({ ...item, obtainedOn: e.target.value || null })} className={LIST_INPUT_CLASS} />
                    <input aria-label={`Note du label ${index + 1}`} value={item.rating ?? ''} readOnly={readOnly} onChange={(e) => update({ ...item, rating: e.target.value || null })} className={LIST_INPUT_CLASS} placeholder="Note (facultatif)" />
                  </>
                )}
              />
              <fieldset className="py-2.5">
                <legend className="text-[13px] text-text-muted">Informations omises (§22 : secret des affaires, information protégée)</legend>
                <div className="mt-1.5 flex flex-wrap gap-x-4 gap-y-1.5">
                  {VSME_DISCLOSURES.filter((code) => code !== 'B1').map((code) => (
                    <label key={code} className="flex items-center gap-1.5 text-[13px] text-text" title={VSME_DISCLOSURE_LABELS[code]}>
                      <input
                        type="checkbox"
                        checked={statement.omittedDisclosures.includes(code)}
                        disabled={readOnly}
                        onChange={(e) =>
                          setField(
                            'omittedDisclosures',
                            e.target.checked ? [...statement.omittedDisclosures, code] : statement.omittedDisclosures.filter((c) => c !== code),
                          )
                        }
                        className="accent-blue-maat"
                      />
                      {code}
                    </label>
                  ))}
                </div>
              </fieldset>
            </DisclosureCard>

            <DisclosureCard code="B2" completeness={byCode('B2')}>
              {yesNo('hasPractices', 'Avez-vous des pratiques en place pour une économie plus durable ?', 'Réduire l’énergie ou l’eau, prévenir la pollution, améliorer les conditions de travail…')}
              {yesNo('hasPolicies', 'Avez-vous des politiques écrites en matière de durabilité ?')}
              {statement.hasPolicies === true && yesNo('policiesPublic', 'Ces politiques sont-elles publiques ?')}
              {yesNo('hasFutureInitiatives', 'Avez-vous des initiatives futures ou des plans en cours ?')}
              {yesNo('hasTargets', 'Suivez-vous leur mise en œuvre par des objectifs ?')}
              <fieldset className="border-b border-border py-2.5">
                <legend className="text-[13px] text-text-muted">Thèmes couverts</legend>
                <div className="mt-1.5 grid gap-1.5 sm:grid-cols-2">
                  {(Object.keys(SUSTAINABILITY_TOPIC_LABELS) as SustainabilityTopic[]).map((topic) => (
                    <label key={topic} className="flex items-center gap-1.5 text-[13px] text-text">
                      <input
                        type="checkbox"
                        checked={statement.coveredTopics.includes(topic)}
                        disabled={readOnly}
                        onChange={(e) =>
                          setField('coveredTopics', e.target.checked ? [...statement.coveredTopics, topic] : statement.coveredTopics.filter((t) => t !== topic))
                        }
                        className="accent-blue-maat"
                      />
                      {SUSTAINABILITY_TOPIC_LABELS[topic]}
                    </label>
                  ))}
                </div>
              </fieldset>
              <TextAreaField id="st-practicesDescription" label="Description (facultatif)" value={statement.practicesDescription} onChange={(v) => setField('practicesDescription', v)} readOnly={readOnly} />
            </DisclosureCard>

            <DisclosureCard code="B3" completeness={byCode('B3')} intro="Émissions calculées selon le GHG Protocol ; Scope 2 selon la méthode fondée sur la localisation.">
              {num('energyConsumptionKwh', 'Consommation totale d’énergie', 'kWh', { hint: microHint ?? 'Calculée automatiquement si la ventilation ci-dessous est complète' })}
              {num('electricityRenewableMwh', 'Électricité renouvelable', 'MWh', { hint: 'Ventilation, si vous la connaissez' })}
              {num('electricityNonRenewableMwh', 'Électricité non renouvelable', 'MWh')}
              {num('fuelsRenewableMwh', 'Combustibles renouvelables', 'MWh')}
              {num('fuelsNonRenewableMwh', 'Combustibles non renouvelables', 'MWh')}
              {num('scope1Tco2e', 'Émissions brutes Scope 1', 'tCO₂eq', { hint: microHint ?? 'Sources détenues ou contrôlées : chaudières, véhicules…' })}
              {num('scope2LocationTco2e', 'Émissions brutes Scope 2', 'tCO₂eq', { hint: microHint ?? 'Énergie achetée : électricité, chaleur…' })}
              {num('co2EmissionsTons', 'Émissions totales Scope 1 et 2', 'tCO₂eq', { hint: 'Calculées automatiquement quand les deux scopes sont saisis' })}
            </DisclosureCard>

            <DisclosureCard code="B4" completeness={byCode('B4')}>
              {yesNo('pollutionReportingApplicable', 'Devez-vous déjà déclarer vos rejets de polluants ?', 'Obligation légale (ICPE, GEREP…) ou système de management environnemental')}
              {statement.pollutionReportingApplicable === true && (
                <>
                  <TextField id="st-pollutionReportUrl" label="Lien vers la déclaration publique (facultatif)" value={statement.pollutionReportUrl} onChange={(v) => setField('pollutionReportUrl', v)} readOnly={readOnly} placeholder="https://" />
                  <ListEditor<Pollutant>
                    label="Polluants rejetés"
                    items={statement.pollutants}
                    empty={{ name: '', medium: 'Air', quantity: 0, unit: 'kg' }}
                    onChange={(items) => setField('pollutants', items)}
                    readOnly={readOnly}
                    addLabel="Ajouter un polluant"
                    renderItem={(item, update, index) => (
                      <>
                        <input aria-label={`Polluant ${index + 1}`} value={item.name} readOnly={readOnly} onChange={(e) => update({ ...item, name: e.target.value })} className={LIST_INPUT_CLASS} placeholder="Polluant" />
                        <select aria-label={`Milieu du polluant ${index + 1}`} value={item.medium} disabled={readOnly} onChange={(e) => update({ ...item, medium: e.target.value as PollutionMedium })} className={LIST_INPUT_CLASS}>
                          {(Object.keys(MEDIUM_LABELS) as PollutionMedium[]).map((medium) => (
                            <option key={medium} value={medium}>
                              {MEDIUM_LABELS[medium]}
                            </option>
                          ))}
                        </select>
                        <input aria-label={`Quantité du polluant ${index + 1}`} type="number" min={0} step="any" value={item.quantity} readOnly={readOnly} onChange={(e) => update({ ...item, quantity: parseFloat(e.target.value) || 0 })} className={LIST_INPUT_CLASS} />
                        <input aria-label={`Unité du polluant ${index + 1}`} value={item.unit} readOnly={readOnly} onChange={(e) => update({ ...item, unit: e.target.value })} className={LIST_INPUT_CLASS} placeholder="kg, t…" />
                      </>
                    )}
                  />
                </>
              )}
            </DisclosureCard>

            <DisclosureCard code="B5" completeness={byCode('B5')} intro="Renseignée site par site, dans la liste des sites de B1 : un site situé dans ou près d’une zone sensible (Natura 2000, ZNIEFF, réserve naturelle…) et le nom de cette zone.">
              <p className="py-1 text-[13px] text-text">
                {sites.length === 0
                  ? 'Aucun site déclaré.'
                  : `${sites.filter((s) => s.inOrNearSensitiveArea === true).length} site(s) sur ${sites.length} dans ou près d’une zone sensible.`}
              </p>
            </DisclosureCard>

            <DisclosureCard code="B6" completeness={byCode('B6')}>
              {num('waterWithdrawalM3', 'Prélèvement total d’eau', 'm³', { hint: microHint })}
              {num('waterConsumptionM3', 'Consommation d’eau des procédés', 'm³', { hint: 'Seulement si vos procédés consomment beaucoup d’eau (prélèvement moins rejets)' })}
              {num('waterConsumptionStressM3', 'dont en zone de stress hydrique', 'm³')}
            </DisclosureCard>

            <DisclosureCard code="B7" completeness={byCode('B7')}>
              {yesNo('circularEconomyApplied', 'Appliquez-vous des principes d’économie circulaire ?', microHint)}
              {statement.circularEconomyApplied === true && (
                <TextAreaField id="st-circularEconomyDescription" label="Comment ?" value={statement.circularEconomyDescription} onChange={(v) => setField('circularEconomyDescription', v)} readOnly={readOnly} />
              )}
              {num('hazardousWasteTons', 'Déchets dangereux', 't', { hint: microHint })}
              {num('nonHazardousWasteTons', 'Déchets non dangereux', 't', { hint: microHint })}
              {num('recyclingRatePct', 'Part recyclée ou préparée pour réutilisation', '%', { hint: microHint })}
              <TextAreaField id="st-materialFlowsDescription" label="Flux annuel des matières utilisées" hint="Seulement dans les secteurs à flux importants : industrie, construction, emballage…" value={statement.materialFlowsDescription} onChange={(v) => setField('materialFlowsDescription', v)} readOnly={readOnly} />
            </DisclosureCard>

            <DisclosureCard code="B8" completeness={byCode('B8')}>
              <SelectField<EmployeeCountUnit>
                id="st-employeeCountUnit"
                label="Unité de décompte"
                value={statement.employeeCountUnit}
                options={[
                  { value: 'Headcount', label: 'Effectif en personnes' },
                  { value: 'FullTimeEquivalent', label: 'Équivalents temps plein' },
                ]}
                onChange={(value) => setField('employeeCountUnit', value)}
                readOnly={readOnly}
              />
              {num('permanentEmployees', 'Contrat permanent (CDI)', 'salariés')}
              {num('temporaryEmployees', 'Contrat temporaire (CDD…)', 'salariés')}
              {num('femaleEmployees', 'Femmes', 'salariées')}
              {num('maleEmployees', 'Hommes', 'salariés')}
              {num('otherGenderEmployees', 'Autre ou non déclaré (facultatif)', 'salariés')}
              <ListEditor<CountryHeadcount>
                label="Effectif par pays (seulement si vous employez dans plusieurs pays)"
                items={statement.employeesByCountry}
                empty={{ country: '', employees: 0 }}
                onChange={(items) => setField('employeesByCountry', items)}
                readOnly={readOnly}
                addLabel="Ajouter un pays"
                renderItem={(item, update, index) => (
                  <>
                    <input aria-label={`Pays ${index + 1}`} value={item.country} readOnly={readOnly} onChange={(e) => update({ ...item, country: e.target.value })} className={LIST_INPUT_CLASS} placeholder="Pays" />
                    <input aria-label={`Effectif du pays ${index + 1}`} type="number" min={0} step="any" value={item.employees} readOnly={readOnly} onChange={(e) => update({ ...item, employees: parseFloat(e.target.value) || 0 })} className={LIST_INPUT_CLASS} />
                  </>
                )}
              />
            </DisclosureCard>

            <DisclosureCard code="B9" completeness={byCode('B9')} intro="Un accident est enregistrable s’il entraîne un décès ou plus de trois jours d’absence.">
              {num('recordableAccidents', 'Accidents du travail enregistrables', 'accidents', { integer: true })}
              {num('hoursWorked', 'Heures travaillées sur l’exercice', 'h', { hint: 'Pour calculer le taux (pour 200 000 heures)' })}
              {num('workFatalities', 'Décès liés au travail', 'décès', { integer: true })}
            </DisclosureCard>

            <DisclosureCard code="B10" completeness={byCode('B10')}>
              {yesNo('minimumWageMet', 'Tous les salariés sont-ils payés au moins au salaire minimum applicable ?', 'SMIC ou minimum de la convention collective')}
              {num('genderPayGapPct', 'Écart de rémunération femmes-hommes', '%', { hint: 'Seulement si la loi vous impose déjà de le publier' })}
              {num('collectiveBargainingPct', 'Salariés couverts par une convention collective', '%')}
              {num('trainingHoursPerEmployee', 'Heures de formation par salarié', 'h/an')}
            </DisclosureCard>

            <DisclosureCard code="B11" completeness={byCode('B11')} intro="Laissez vide s’il n’y a eu aucune condamnation sur l’exercice.">
              <NumberField id="st-corruptionConvictions" label="Condamnations pour corruption" unit="condamnations" value={statement.corruptionConvictions} onChange={(v) => setField('corruptionConvictions', v)} readOnly={readOnly} integer />
              <NumberField id="st-corruptionFinesEur" label="Montant total des amendes" unit="€" value={statement.corruptionFinesEur} onChange={(v) => setField('corruptionFinesEur', v)} readOnly={readOnly} />
            </DisclosureCard>
          </section>

          <section aria-labelledby="complements-heading" className="flex flex-col gap-4">
            <div>
              <h2 id="complements-heading" className="text-[16px] font-semibold text-text">
                Compléments
              </h2>
              <p className="text-[13px] text-text-muted">Indicateurs suivis dans MAAT en plus de ceux que demande la norme. Ils figurent à la fin de la section 04 du rapport.</p>
            </div>
            <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
              {COMPLEMENT_GROUPS.map((group) => {
                const Icon = group.icon
                return (
                  <Card key={group.id} as="section" aria-labelledby={`domain-${group.id}`}>
                    <div className="mb-4 flex items-center gap-3">
                      <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg" style={{ backgroundColor: group.bgColor }} aria-hidden="true">
                        <Icon size={16} style={{ color: group.color }} strokeWidth={1.75} />
                      </div>
                      <h3 id={`domain-${group.id}`} className="text-[14px] font-semibold text-text">
                        {group.label}
                      </h3>
                    </div>
                    <div>
                      {group.metrics.map((metric) => (
                        <MetricRow
                          key={metric.key}
                          metric={metric}
                          value={indicators[metric.key] as number | null}
                          prevValue={prev(metric.key)}
                          onChange={setIndicator}
                          readOnly={readOnly}
                        />
                      ))}
                    </div>
                  </Card>
                )
              })}
            </div>
          </section>

          {saveButton && <div className="flex justify-end">{saveButton}</div>}
        </>
      )}
    </div>
  )
}

// Même complétude que la section 04 du rapport (VsmeCompleteness, côté serveur).
function CompletenessBanner({ completeness, year }: { completeness: VsmeCompleteness; year: number }) {
  const incomplete = completeness.disclosures.filter((d) => d.state === 'Incomplete')
  const compliant = completeness.isCompliant

  return (
    <div
      role="status"
      className={`rounded-xl border-l-4 px-4 py-3 ${compliant ? 'border-green-maat bg-kpi-green' : 'border-orange bg-kpi-amber'}`}
    >
      <p className={`text-[13.5px] font-semibold ${compliant ? 'text-green-maat-text' : 'text-amber'}`}>
        {completeness.completeCount} information{completeness.completeCount > 1 ? 's' : ''} sur 11 complète
        {completeness.completeCount > 1 ? 's' : ''} pour {year}
      </p>
      <p className="mt-0.5 text-[13px] text-text">
        {compliant
          ? 'Votre rapport déclarera sa conformité au module de base de la norme volontaire (règlement délégué (UE) 2026/1560).'
          : 'Complétez les informations suivantes pour que votre rapport puisse déclarer sa conformité au module de base :'}
      </p>
      {!compliant && (
        <ul className="mt-1.5 flex flex-wrap gap-2">
          {incomplete.map((d) => (
            <li key={d.disclosure}>
              <a href={`#vsme-${d.disclosure}`} className="rounded-md bg-white px-2 py-0.5 text-[12.5px] font-medium text-blue-maat-text hover:underline">
                {d.disclosure} · {VSME_DISCLOSURE_LABELS[d.disclosure]}
              </a>
            </li>
          ))}
        </ul>
      )}
      {completeness.isMicro && (
        <p className="mt-1.5 text-[12px] font-light text-text-muted">
          10 salariés ou moins : l’énergie, les émissions, l’eau et les déchets sont facultatifs.
        </p>
      )}
    </div>
  )
}
