import { type KeyboardEvent, useCallback, useEffect, useRef, useState } from 'react'
import { Check, Leaf, ShoppingCart, TrendingUp, Users } from 'lucide-react'
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
import { DatePicker } from '../components/ui/DatePicker'
import { PageHeader } from '../components/ui/PageHeader'
import { Select } from '../components/ui/Select'
import { useEntitlements } from '../billing/entitlements'
import { UpgradeNotice } from '../components/billing/UpgradeNotice'
import { SitesEditor } from '../components/indicators/SitesEditor'
import { VsmeProgress } from '../components/indicators/VsmeProgress'
import {
  ChipToggleGroup,
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
  VSME_DATA_SOURCES,
  VSME_DISCLOSURES,
  VSME_GROUPS,
  type VsmeTab,
} from '../constants/vsme'

// docs/specs/norme-volontaire.md, section 5 : les informations B1 à B11 du module de base de la
// norme volontaire (ex-VSME), en onglets qui suivent les groupes de la norme, avec un sommaire
// qui porte la complétude ; puis les compléments propres à MAAT. Mêmes libellés et mêmes unités
// que la section 04 du rapport PDF (ReportSustainabilityBuilder.cs, ReportIndicatorCatalog.cs).

// ─── Compléments (hors norme) ────────────────────────────────────────────────

interface MetricConfig {
  key: keyof RseIndicators
  label: string
  unit: string
  integer?: boolean
}

// Couleurs de domaine de la charte (skill charte-maat, « Graphiques »), jamais réattribuées.
const COMPLEMENT_GROUPS: { id: string; label: string; icon: typeof Leaf; iconClass: string; metrics: MetricConfig[] }[] = [
  {
    id: 'env',
    label: 'Environnement',
    icon: Leaf,
    iconClass: 'bg-green-maat/10 text-green-maat-text',
    metrics: [{ key: 'renewableEnergyPct', label: 'Part énergie renouvelable', unit: '%' }],
  },
  {
    id: 'social',
    label: 'Social',
    icon: Users,
    iconClass: 'bg-chart-social/10 text-chart-social',
    metrics: [
      { key: 'turnoverRatePct', label: 'Taux de turnover', unit: '%' },
      { key: 'workAccidentRate', label: 'Taux de fréquence des accidents', unit: '/ million h' },
      { key: 'genderEqualityIndex', label: 'Index égalité F/H', unit: '/100' },
      { key: 'permanentContractPct', label: 'Part CDI', unit: '%' },
    ],
  },
  {
    id: 'achats',
    label: 'Achats responsables',
    icon: ShoppingCart,
    iconClass: 'bg-chart-achats/15 text-text',
    metrics: [
      { key: 'localSuppliersPct', label: 'Fournisseurs locaux (< 100 km)', unit: '%' },
      { key: 'rseAssessedSuppliersPct', label: 'Fournisseurs évalués RSE', unit: '%' },
      { key: 'activeSuppliersCount', label: 'Nombre de fournisseurs actifs', unit: 'fournisseurs', integer: true },
    ],
  },
  {
    id: 'eco',
    label: 'Économique',
    icon: TrendingUp,
    iconClass: 'bg-blue-maat/10 text-blue-maat-text',
    metrics: [
      { key: 'rseInvestmentEur', label: 'Investissements RSE', unit: '€' },
      { key: 'exportRevenuePct', label: 'Part CA export', unit: '%' },
    ],
  },
]

const TABS: { id: VsmeTab; label: string; codes: VsmeDisclosure[] }[] = [
  ...VSME_GROUPS.map((g) => ({ id: g.id as VsmeTab, label: g.label, codes: g.codes })),
  { id: 'complements', label: 'Compléments', codes: [] },
]

const CURRENT_YEAR = new Date().getFullYear()
const YEAR_OPTIONS = Array.from({ length: 6 }, (_, i) => CURRENT_YEAR - i)

const MEDIUM_LABELS: Record<PollutionMedium, string> = { Air: 'Air', Water: 'Eau', Soil: 'Sol' }

type SaveStatus = 'idle' | 'dirty' | 'saving' | 'saved' | 'error'

export function IndicatorsPage() {
  const [year, setYear] = useState(CURRENT_YEAR)
  // docs/specs/abonnement.md, section 8 : saisie ouverte dès Essential (norme-volontaire.md).
  // Les valeurs déjà saisies (offre précédente) restent consultables.
  const { canEditIndicators } = useEntitlements()
  const readOnly = !canEditIndicators
  const [tab, setTab] = useState<VsmeTab>('general')
  const [indicators, setIndicators] = useState<RseIndicators>(EMPTY_INDICATORS)
  const [prevIndicators, setPrevIndicators] = useState<RseIndicators | null>(null)
  const [statement, setStatement] = useState<VsmeStatement>(EMPTY_STATEMENT)
  const [sites, setSites] = useState<CompanySite[]>([])
  const [completeness, setCompleteness] = useState<VsmeCompleteness | null>(null)
  const [loadStatus, setLoadStatus] = useState<'loading' | 'ready' | 'error'>('loading')
  const [saveStatus, setSaveStatus] = useState<SaveStatus>('idle')
  const [saveError, setSaveError] = useState<string | null>(null)
  const savedTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  // Information à faire défiler jusqu'à l'écran une fois son onglet affiché (sommaire).
  const [pendingScroll, setPendingScroll] = useState<VsmeDisclosure | null>(null)
  const tabRefs = useRef<Partial<Record<VsmeTab, HTMLButtonElement | null>>>({})

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

  useEffect(() => {
    if (!pendingScroll) return
    document.getElementById(`vsme-${pendingScroll}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    setPendingScroll(null)
  }, [pendingScroll, tab])

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

  function goTo(code: VsmeDisclosure) {
    const group = VSME_GROUPS.find((g) => g.codes.includes(code))
    if (group) setTab(group.id)
    setPendingScroll(code)
  }

  // Onglets au clavier : flèches gauche et droite, Début et Fin (motif ARIA « tabs »).
  function handleTabKeyDown(e: KeyboardEvent<HTMLButtonElement>) {
    const index = TABS.findIndex((t) => t.id === tab)
    const next =
      e.key === 'ArrowRight' ? (index + 1) % TABS.length
      : e.key === 'ArrowLeft' ? (index - 1 + TABS.length) % TABS.length
      : e.key === 'Home' ? 0
      : e.key === 'End' ? TABS.length - 1
      : null
    if (next === null) return
    e.preventDefault()
    setTab(TABS[next].id)
    tabRefs.current[TABS[next].id]?.focus()
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
  const micro = completeness?.isMicro ?? false
  const prev = (key: keyof RseIndicators) => (prevIndicators ? (prevIndicators[key] as number | null) : null)

  // Raccourci pour un champ chiffré de RseIndicators.
  const num = (key: keyof RseIndicators, label: string, unit: string, options: { hint?: string; integer?: boolean; optional?: boolean } = {}) => (
    <NumberField
      source={VSME_DATA_SOURCES[key]}
      id={`ind-${key}`}
      label={label}
      unit={unit}
      value={indicators[key] as number | null}
      previous={prev(key)}
      onChange={(value) => setIndicator(key, value)}
      readOnly={readOnly}
      hint={options.hint}
      integer={options.integer}
      optional={options.optional}
    />
  )

  const yesNo = (key: keyof VsmeStatement, label: string, options: { hint?: string; optional?: boolean } = {}) => (
    <YesNoField
      name={`st-${key}`}
      label={label}
      value={statement[key] as boolean | null}
      onChange={(value) => setField(key, value as never)}
      readOnly={readOnly}
      hint={options.hint}
      optional={options.optional}
    />
  )

  const tabProgress = (codes: VsmeDisclosure[]) => {
    const done = codes.filter((code) => byCode(code)?.state !== 'Incomplete').length
    return { done, total: codes.length }
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <PageHeader
          title="Indicateurs RSE"
          subtitle="Informations de durabilité de l’exercice, selon la norme volontaire européenne (ex-VSME)"
        />
        <div className="flex items-center gap-2 text-[13px] text-text-muted">
          <span aria-hidden>Exercice</span>
          <Select
            aria-label="Sélectionner l'année"
            value={String(year)}
            options={YEAR_OPTIONS.map((y) => ({ value: String(y), label: String(y) }))}
            onChange={(value) => setYear(Number(value))}
            className="w-28 font-medium"
          />
        </div>
      </div>

      {!canEditIndicators && (
        <UpgradeNotice requiredPlan="Essential" title="Préparez votre rapport selon la norme volontaire">
          Énergie, émissions, effectifs, sites : saisissez les informations B1 à B11 chaque année pour les retrouver dans votre
          rapport, prêt à transmettre à vos clients et à votre banque.
        </UpgradeNotice>
      )}

      {loadStatus === 'loading' && (
        <Card>
          <p role="status" className="py-10 text-center text-[13px] text-text-muted">
            Chargement…
          </p>
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
        <div className="grid items-start gap-6 lg:grid-cols-[272px_minmax(0,1fr)]">
          <aside className="lg:sticky lg:top-6">
            <VsmeProgress completeness={completeness} year={year} onSelect={goTo} />
          </aside>

          <div className="flex min-w-0 flex-col gap-5">
            <h2 className="sr-only">Informations de durabilité (norme volontaire)</h2>
            <div role="tablist" aria-label="Groupes d’informations" className="flex gap-1 overflow-x-auto rounded-xl border border-border bg-white p-1">
              {TABS.map((t) => {
                const selected = t.id === tab
                const progress = tabProgress(t.codes)
                return (
                  <button
                    key={t.id}
                    ref={(el) => {
                      tabRefs.current[t.id] = el
                    }}
                    type="button"
                    role="tab"
                    id={`tab-${t.id}`}
                    aria-selected={selected}
                    aria-controls={`panel-${t.id}`}
                    tabIndex={selected ? 0 : -1}
                    onClick={() => setTab(t.id)}
                    onKeyDown={handleTabKeyDown}
                    className={`flex shrink-0 items-center gap-2 rounded-lg px-4 py-2 text-[13px] font-medium transition-colors ${
                      selected ? 'bg-blue-maat text-white shadow-button-primary' : 'text-text-muted hover:bg-bg hover:text-text'
                    }`}
                  >
                    {t.label}
                    {t.codes.length > 0 && (
                      <span
                        className={`rounded-full px-1.5 text-[11px] tabular-nums ${
                          selected ? 'bg-white/20 text-white' : progress.done === progress.total ? 'bg-green-maat/15 text-green-maat-text' : 'bg-bg text-text-muted'
                        }`}
                      >
                        {progress.done === progress.total ? <Check className="inline h-3 w-3" aria-label="complet" /> : `${progress.done}/${progress.total}`}
                      </span>
                    )}
                  </button>
                )
              })}
            </div>

            <div role="tabpanel" id={`panel-${tab}`} aria-labelledby={`tab-${tab}`} className="flex flex-col gap-5">
              {tab === 'general' && (
                <>
                  <DisclosureCard code="B1" completeness={byCode('B1')}>
                    <SelectField<ReportingBasis>
                      id="st-reportingBasis"
                      label="Base d’établissement"
                      value={statement.reportingBasis}
                      options={[
                        { value: 'Individual', label: 'Individuelle (l’entreprise seule)' },
                        { value: 'Consolidated', label: 'Consolidée (avec les filiales)' },
                      ]}
                      onChange={(value) => setField('reportingBasis', value)}
                      readOnly={readOnly}
                    />
                    <TextField id="st-legalForm" label="Forme juridique" value={statement.legalForm} onChange={(v) => setField('legalForm', v)} readOnly={readOnly} suggestions={LEGAL_FORM_SUGGESTIONS} placeholder="SAS, SARL…" />
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
                    <NumberField id="st-totalAssetsEur" label="Total du bilan" unit="€" source={VSME_DATA_SOURCES.totalAssetsEur} value={statement.totalAssetsEur} onChange={(v) => setField('totalAssetsEur', v)} readOnly={readOnly} />
                    {num('revenueEur', 'Chiffre d’affaires', '€')}
                    {num('employeeCountFte', 'Effectif en ETP', 'ETP', { hint: 'Utilisé si la répartition par contrat (B8) n’est pas renseignée' })}
                    <TextField id="st-primaryCountry" label="Pays principal d’activité" value={statement.primaryCountry} onChange={(v) => setField('primaryCountry', v)} readOnly={readOnly} placeholder="France" hint="Le code NACE est déduit de votre code NAF." />
                    <SitesEditor sites={sites} onChange={handleSitesChange} readOnly={readOnly} />
                    <ListEditor<Certification>
                      label="Certifications et labels de durabilité"
                      hint="Facultatif : ISO 14001, B Corp, Lucie, EcoVadis…"
                      items={statement.certifications}
                      empty={{ name: '', issuer: null, obtainedOn: null, rating: null }}
                      onChange={(items) => setField('certifications', items)}
                      readOnly={readOnly}
                      addLabel="Ajouter un label"
                      renderItem={(item, update, index) => (
                        <>
                          <input aria-label={`Label ${index + 1}`} value={item.name} readOnly={readOnly} onChange={(e) => update({ ...item, name: e.target.value })} className={LIST_INPUT_CLASS} placeholder="Label" />
                          <input aria-label={`Organisme du label ${index + 1}`} value={item.issuer ?? ''} readOnly={readOnly} onChange={(e) => update({ ...item, issuer: e.target.value || null })} className={LIST_INPUT_CLASS} placeholder="Organisme" />
                          <DatePicker aria-label={`Date du label ${index + 1}`} value={item.obtainedOn} disabled={readOnly} onChange={(value) => update({ ...item, obtainedOn: value })} placeholder="Date d’obtention" />
                          <input aria-label={`Note du label ${index + 1}`} value={item.rating ?? ''} readOnly={readOnly} onChange={(e) => update({ ...item, rating: e.target.value || null })} className={LIST_INPUT_CLASS} placeholder="Note" />
                        </>
                      )}
                    />
                    <ChipToggleGroup<VsmeDisclosure>
                      label="Informations omises"
                      hint="§22 : secret des affaires ou information protégée. Une information omise est annoncée comme telle dans le rapport."
                      options={VSME_DISCLOSURES.filter((code) => code !== 'B1').map((code) => ({ value: code, label: code, title: VSME_DISCLOSURE_LABELS[code] }))}
                      selected={statement.omittedDisclosures}
                      onChange={(value) => setField('omittedDisclosures', value)}
                      readOnly={readOnly}
                    />
                  </DisclosureCard>

                  <DisclosureCard code="B2" completeness={byCode('B2')}>
                    {yesNo('hasPractices', 'Avez-vous des pratiques en place pour une économie plus durable ?', { hint: 'Réduire l’énergie ou l’eau, prévenir la pollution, améliorer les conditions de travail…' })}
                    {yesNo('hasPolicies', 'Avez-vous des politiques écrites en matière de durabilité ?')}
                    {statement.hasPolicies === true && yesNo('policiesPublic', 'Ces politiques sont-elles publiques ?')}
                    {yesNo('hasFutureInitiatives', 'Avez-vous des initiatives futures ou des plans en cours ?')}
                    {yesNo('hasTargets', 'Suivez-vous leur mise en œuvre par des objectifs ?')}
                    <ChipToggleGroup<SustainabilityTopic>
                      label="Thèmes couverts"
                      options={(Object.keys(SUSTAINABILITY_TOPIC_LABELS) as SustainabilityTopic[]).map((topic) => ({ value: topic, label: SUSTAINABILITY_TOPIC_LABELS[topic] }))}
                      selected={statement.coveredTopics}
                      onChange={(value) => setField('coveredTopics', value)}
                      readOnly={readOnly}
                    />
                    <TextAreaField id="st-practicesDescription" label="Description" hint="Facultatif : ce que vous faites, en quelques phrases." value={statement.practicesDescription} onChange={(v) => setField('practicesDescription', v)} readOnly={readOnly} />
                  </DisclosureCard>
                </>
              )}

              {tab === 'environment' && (
                <>
                  <DisclosureCard code="B3" completeness={byCode('B3')} intro="Émissions calculées selon le GHG Protocol ; Scope 2 selon la méthode fondée sur la localisation.">
                    {num('energyConsumptionKwh', 'Consommation totale d’énergie', 'kWh', { optional: micro, hint: 'Calculée si la ventilation ci-dessous est complète' })}
                    {num('co2EmissionsTons', 'Émissions totales Scope 1 et 2', 'tCO₂eq', { hint: 'Calculées quand les deux scopes sont saisis' })}
                    {num('scope1Tco2e', 'Émissions brutes Scope 1', 'tCO₂eq', { optional: micro, hint: 'Sources détenues ou contrôlées : chaudières, véhicules…' })}
                    {num('scope2LocationTco2e', 'Émissions brutes Scope 2', 'tCO₂eq', { optional: micro, hint: 'Énergie achetée : électricité, chaleur…' })}
                    <p className="border-t border-border pt-4 text-[12px] font-semibold uppercase tracking-[0.06em] text-text-muted sm:col-span-2">
                      Ventilation, si vous la connaissez
                    </p>
                    {num('electricityRenewableMwh', 'Électricité renouvelable', 'MWh')}
                    {num('electricityNonRenewableMwh', 'Électricité non renouvelable', 'MWh')}
                    {num('fuelsRenewableMwh', 'Combustibles renouvelables', 'MWh')}
                    {num('fuelsNonRenewableMwh', 'Combustibles non renouvelables', 'MWh')}
                  </DisclosureCard>

                  <DisclosureCard code="B4" completeness={byCode('B4')}>
                    {yesNo('pollutionReportingApplicable', 'Devez-vous déjà déclarer vos rejets de polluants ?', { hint: 'Obligation légale (ICPE, GEREP…) ou système de management environnemental' })}
                    {statement.pollutionReportingApplicable === true && (
                      <>
                        <TextField id="st-pollutionReportUrl" label="Lien vers la déclaration publique" hint="Facultatif si vous listez les polluants" value={statement.pollutionReportUrl} onChange={(v) => setField('pollutionReportUrl', v)} readOnly={readOnly} placeholder="https://" wide />
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
                              <Select<PollutionMedium>
                                aria-label={`Milieu du polluant ${index + 1}`}
                                value={item.medium}
                                disabled={readOnly}
                                onChange={(medium) => update({ ...item, medium })}
                                options={(Object.keys(MEDIUM_LABELS) as PollutionMedium[]).map((medium) => ({ value: medium, label: MEDIUM_LABELS[medium] }))}
                              />
                              <input aria-label={`Quantité du polluant ${index + 1}`} type="number" min={0} step="any" value={item.quantity} readOnly={readOnly} onChange={(e) => update({ ...item, quantity: parseFloat(e.target.value) || 0 })} className={LIST_INPUT_CLASS} />
                              <input aria-label={`Unité du polluant ${index + 1}`} value={item.unit} readOnly={readOnly} onChange={(e) => update({ ...item, unit: e.target.value })} className={LIST_INPUT_CLASS} placeholder="kg, t…" />
                            </>
                          )}
                        />
                      </>
                    )}
                  </DisclosureCard>

                  <DisclosureCard code="B5" completeness={byCode('B5')} intro="Renseignée site par site, dans la liste des sites de B1 (onglet Général). Sans réponse de votre part, MAAT la déduit des bases publiques de l’INPN (zones à moins de 500 m).">
                    <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-border bg-bg/50 px-4 py-3 sm:col-span-2">
                      <p className="text-[13px] text-text">
                        {sites.length === 0
                          ? 'Aucun site déclaré.'
                          : `${sites.filter((s) => s.effectiveInOrNearSensitiveArea === true).length} site(s) sur ${sites.length} dans ou près d’une zone sensible.`}
                      </p>
                      <button type="button" onClick={() => goTo('B1')} className="text-[13px] font-medium text-blue-maat-text hover:underline">
                        Gérer les sites
                      </button>
                    </div>
                  </DisclosureCard>

                  <DisclosureCard code="B6" completeness={byCode('B6')}>
                    {num('waterWithdrawalM3', 'Prélèvement total d’eau', 'm³', { optional: micro })}
                    <div className="hidden sm:block" />
                    {num('waterConsumptionM3', 'Consommation d’eau des procédés', 'm³', { hint: 'Seulement si vos procédés consomment beaucoup d’eau' })}
                    {num('waterConsumptionStressM3', 'dont en zone de stress hydrique', 'm³')}
                  </DisclosureCard>

                  <DisclosureCard code="B7" completeness={byCode('B7')}>
                    {yesNo('circularEconomyApplied', 'Appliquez-vous des principes d’économie circulaire ?', { optional: micro })}
                    {statement.circularEconomyApplied === true && (
                      <TextAreaField id="st-circularEconomyDescription" label="Comment ?" value={statement.circularEconomyDescription} onChange={(v) => setField('circularEconomyDescription', v)} readOnly={readOnly} />
                    )}
                    {num('hazardousWasteTons', 'Déchets dangereux', 't', { optional: micro })}
                    {num('nonHazardousWasteTons', 'Déchets non dangereux', 't', { optional: micro })}
                    {num('recyclingRatePct', 'Part recyclée ou réutilisée', '%', { optional: micro })}
                    <div className="hidden sm:block" />
                    <TextAreaField id="st-materialFlowsDescription" label="Flux annuel des matières utilisées" hint="Seulement dans les secteurs à flux importants : industrie, construction, emballage…" value={statement.materialFlowsDescription} onChange={(v) => setField('materialFlowsDescription', v)} readOnly={readOnly} />
                  </DisclosureCard>
                </>
              )}

              {tab === 'social' && (
                <>
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
                    <div className="hidden sm:block" />
                    {num('permanentEmployees', 'Contrat permanent (CDI)', 'salariés')}
                    {num('temporaryEmployees', 'Contrat temporaire (CDD…)', 'salariés')}
                    {num('femaleEmployees', 'Femmes', 'salariées')}
                    {num('maleEmployees', 'Hommes', 'salariés')}
                    {num('otherGenderEmployees', 'Autre ou non déclaré', 'salariés', { hint: 'Facultatif' })}
                    <div className="hidden sm:block" />
                    <ListEditor<CountryHeadcount>
                      label="Effectif par pays"
                      hint="Seulement si vous employez dans plusieurs pays."
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
                    {num('workFatalities', 'Décès liés au travail', 'décès', { integer: true })}
                    {num('hoursWorked', 'Heures travaillées sur l’exercice', 'h', { hint: 'Pour calculer le taux (pour 200 000 heures)' })}
                  </DisclosureCard>

                  <DisclosureCard code="B10" completeness={byCode('B10')}>
                    {yesNo('minimumWageMet', 'Tous les salariés sont-ils payés au moins au salaire minimum applicable ?', { hint: 'SMIC ou minimum de la convention collective' })}
                    {num('collectiveBargainingPct', 'Salariés couverts par une convention collective', '%')}
                    {num('trainingHoursPerEmployee', 'Heures de formation par salarié', 'h/an')}
                    {num('genderPayGapPct', 'Écart de rémunération femmes-hommes', '%', { hint: 'Seulement si la loi vous impose déjà de le publier' })}
                  </DisclosureCard>
                </>
              )}

              {tab === 'governance' && (
                <DisclosureCard code="B11" completeness={byCode('B11')} intro="Laissez vide s’il n’y a eu aucune condamnation sur l’exercice.">
                  <NumberField id="st-corruptionConvictions" label="Condamnations pour corruption" unit="condamnations" value={statement.corruptionConvictions} onChange={(v) => setField('corruptionConvictions', v)} readOnly={readOnly} integer />
                  <NumberField id="st-corruptionFinesEur" label="Montant total des amendes" unit="€" value={statement.corruptionFinesEur} onChange={(v) => setField('corruptionFinesEur', v)} readOnly={readOnly} />
                </DisclosureCard>
              )}

              {tab === 'complements' && (
                <>
                  <p className="text-[13px] text-text-muted">
                    Indicateurs suivis dans MAAT en plus de ceux que demande la norme. Ils figurent à la fin de la section 04 du rapport.
                  </p>
                  <div className="grid gap-5 xl:grid-cols-2">
                    {COMPLEMENT_GROUPS.map((group) => {
                      const Icon = group.icon
                      return (
                        <Card key={group.id} as="section" variant="flat" aria-labelledby={`complement-${group.id}`} className="p-6">
                          <div className="flex items-center gap-3">
                            <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${group.iconClass}`} aria-hidden>
                              <Icon className="h-4 w-4" />
                            </span>
                            <h3 id={`complement-${group.id}`} className="font-heading text-[15px] font-semibold text-text">
                              {group.label}
                            </h3>
                          </div>
                          <div className="mt-5 grid gap-5">
                            {group.metrics.map((metric) => (
                              <div key={metric.key}>{num(metric.key, metric.label, metric.unit, { integer: metric.integer })}</div>
                            ))}
                          </div>
                        </Card>
                      )
                    })}
                  </div>
                </>
              )}
            </div>

            {canEditIndicators && (
              <div className="sticky bottom-4 z-10 flex items-center justify-between gap-3 rounded-xl border border-border bg-white/95 px-4 py-2.5 shadow-popover backdrop-blur">
                <p className="min-w-0 text-[13px] text-text-muted" aria-live="polite">
                  {saveStatus === 'dirty' && 'Modifications non enregistrées'}
                  {saveStatus === 'saving' && 'Enregistrement…'}
                  {saveStatus === 'saved' && <span className="text-green-maat-text">Enregistré</span>}
                  {saveStatus === 'error' && saveError && (
                    <span role="alert" className="text-red">
                      {saveError}
                    </span>
                  )}
                  {saveStatus === 'idle' && <span className="hidden sm:inline">Les sites s’enregistrent un par un ; le reste, avec ce bouton.</span>}
                </p>
                <button
                  type="button"
                  onClick={() => void handleSave()}
                  disabled={saveStatus !== 'dirty' && saveStatus !== 'error'}
                  className="shrink-0 rounded-button bg-blue-maat px-5 py-2 text-[13.5px] font-semibold text-white shadow-button-primary transition-colors hover:bg-blue-maat-text disabled:cursor-not-allowed disabled:opacity-40"
                >
                  Enregistrer
                </button>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
