import { Check, CloudCheck } from 'lucide-react'
import { ANSWER_SCALE } from '../../constants/answerScale'
import { EFFORT_LABELS } from '../../constants/effortLabels'
import { getScoreLabel } from '../../constants/scoreLabels'
import { DEMO_DOMAIN_SCORES, DOMAINS, SECTORS, weightedScore } from '../content'

// Maquettes statiques des écrans de l'application, pour la visite produit. Les textes des
// questions et des recommandations sont ceux du référentiel (backend/MAAT.Infrastructure/
// Seed/questions.csv et recommendations.csv), les libellés ceux des constantes du frontend.

const domain = (key: (typeof DOMAINS)[number]['key']) => DOMAINS.find((d) => d.key === key)!

function Frame({ children }: { children: React.ReactNode }) {
  return (
    <div className="rounded-card border border-border bg-white p-5 sm:p-7" aria-hidden="true">
      {children}
    </div>
  )
}

export function QuestionnaireMock() {
  const selected = 3
  return (
    <Frame>
      <div className="flex gap-1.5">
        {DOMAINS.map((d, i) => (
          <span key={d.key} className="h-1 flex-1 rounded-full" style={{ background: i === 0 ? d.color : 'var(--color-border)' }} />
        ))}
      </div>
      <div className="mt-4 flex items-center justify-between text-[12px] text-text-muted">
        <span>Environnement</span>
        <span className="numeric">Question 1 sur 45<span className="max-sm:hidden"> · environ 28 min restantes</span></span>
      </div>
      <p className="mt-6 font-heading text-[19px] font-semibold leading-snug text-text sm:text-[21px]">
        Relevez-vous chaque année vos consommations d’énergie&nbsp;?
      </p>
      <p className="mt-2 text-[13px] leading-relaxed text-text-muted">
        Électricité, gaz ou fioul de vos locaux, carburant de vos véhicules. Un relevé annuel à partir
        des factures suffit, aucun outil dédié n’est nécessaire.
      </p>
      <ul className="mt-5 grid gap-2 sm:grid-cols-2">
        {ANSWER_SCALE.map((option) => {
          const isSelected = option.value === selected
          return (
            <li
              key={option.value}
              className={`flex items-center gap-3 rounded-button border px-3.5 py-2.5 text-[13px] ${isSelected ? 'border-blue-maat bg-kpi-blue font-medium text-text' : 'border-border text-text-muted'}`}
            >
              <span className={`flex h-4 w-4 shrink-0 items-center justify-center rounded-full border ${isSelected ? 'border-blue-maat bg-blue-maat' : 'border-border-strong'}`}>
                {isSelected && <span className="h-1.5 w-1.5 rounded-full bg-white" />}
              </span>
              {option.label}
            </li>
          )
        })}
      </ul>
      <p className="mt-5 flex items-center gap-2 text-[12px] text-green-maat-text">
        <CloudCheck size={14} /> Réponse enregistrée automatiquement
      </p>
    </Frame>
  )
}

const RECOMMENDATIONS = [
  { domain: 'Governance', title: 'Confier le suivi RSE à une personne', impact: 7.5, effort: 'Medium' },
  { domain: 'Environmental', title: 'Estimer vos émissions avec un calculateur de votre fédération', impact: 5.45, effort: 'Medium' },
  { domain: 'Social', title: 'Reprendre votre document d’évaluation des risques poste par poste', impact: 5.45, effort: 'Medium' },
  { domain: 'Environmental', title: 'Totaliser vos consommations d’énergie une fois par an', impact: 3.64, effort: 'Low' },
] as const

const formatPoints = (n: number) => n.toLocaleString('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export function RecommendationsMock() {
  return (
    <Frame>
      <div className="flex items-baseline justify-between">
        <p className="font-heading text-[15px] font-semibold text-text">Recommandations priorisées</p>
        <p className="text-[12px] text-text-muted">Triées par gain de score</p>
      </div>
      <ol className="mt-4 divide-y divide-border border-y border-border">
        {RECOMMENDATIONS.map((r, i) => {
          const d = domain(r.domain)
          return (
            <li key={r.title} className="grid grid-cols-[1.5rem_1fr_auto] items-start gap-3 py-3.5">
              <span className="numeric pt-0.5 font-heading text-[13px] text-text-muted">{String(i + 1).padStart(2, '0')}</span>
              <div className="min-w-0">
                <p className="text-[13.5px] font-medium leading-snug text-text">{r.title}</p>
                <p className="mt-1 flex items-center gap-2 text-[11.5px] text-text-muted">
                  <span className="h-2 w-2 rounded-full" style={{ background: d.color }} />
                  {d.label} · {EFFORT_LABELS[r.effort]}
                </p>
              </div>
              <span className="numeric rounded-md bg-kpi-green px-2 py-1 text-[12px] font-semibold text-green-maat-text">
                +{formatPoints(r.impact)} pts
              </span>
            </li>
          )
        })}
      </ol>
    </Frame>
  )
}

const PLAN = [
  { title: 'Confier le suivi RSE à une personne', owner: 'C. Lemoine', due: '15 oct.', status: 'Done' },
  { title: 'Estimer vos émissions avec un calculateur', owner: 'J. Moreau', due: '30 nov.', status: 'InProgress' },
  { title: 'Reprendre l’évaluation des risques', owner: 'S. Bernard', due: '12 déc.', status: 'Blocked' },
  { title: 'Écrire une fiche d’accueil sécurité', owner: 'S. Bernard', due: '20 janv.', status: 'Planned' },
] as const

// Mêmes libellés que PlanActionsPage. « Bloqué » passe par --color-amber plutôt que
// --color-orange : l'orange de la charte ne porte pas de texte (charte-maat-v2.md, section 2).
const STATUS = {
  Planned: { label: 'Planifié', classes: 'border-border bg-bg text-text-muted' },
  InProgress: { label: 'En cours', classes: 'border-blue-maat/40 bg-blue-maat/10 text-blue-maat-text' },
  Blocked: { label: 'Bloqué', classes: 'border-amber/40 bg-kpi-amber text-amber' },
  Done: { label: 'Terminé', classes: 'border-green-maat/40 bg-kpi-green text-green-maat-text' },
}

export function ActionPlanMock() {
  return (
    <Frame>
      <div className="flex items-baseline justify-between">
        <p className="font-heading text-[15px] font-semibold text-text">Plan d’actions</p>
        <p className="numeric text-[12px] text-text-muted">1 terminée sur 4</p>
      </div>
      <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-bg">
        <span className="block h-full w-1/4 rounded-full bg-green-maat" />
      </div>
      <ul className="mt-5 flex flex-col gap-2">
        {PLAN.map((a) => (
          <li key={a.title} className="grid grid-cols-[1fr_auto] items-center gap-3 rounded-button border border-border px-3.5 py-3 sm:grid-cols-[1fr_6.5rem_4rem_5.5rem]">
            <span className={`truncate text-[13px] ${a.status === 'Done' ? 'text-text-muted line-through' : 'text-text'}`}>{a.title}</span>
            <span className="hidden truncate text-[12px] text-text-muted sm:block">{a.owner}</span>
            <span className="numeric hidden text-[12px] text-text-muted sm:block">{a.due}</span>
            <span className={`justify-self-end rounded-full border px-2.5 py-0.5 text-[11.5px] font-medium ${STATUS[a.status].classes}`}>
              {STATUS[a.status].label}
            </span>
          </li>
        ))}
      </ul>
    </Frame>
  )
}

export function ReportMock() {
  const sector = SECTORS[0]
  const score = weightedScore(DEMO_DOMAIN_SCORES, sector.weights)
  return (
    <div className="relative mx-auto flex max-w-[460px] justify-center py-4" aria-hidden="true">
      {/* Deuxième page, en retrait : suggère le document sans le détailler. */}
      <div className="absolute top-0 left-1/2 aspect-[210/297] w-[78%] translate-x-[-42%] rotate-[3deg] rounded-[4px] border border-border bg-white" />
      <div className="relative aspect-[210/297] w-[78%] rounded-[4px] border border-border bg-white p-[7%] shadow-popover">
        <div className="flex h-full flex-col">
          <div className="flex items-center gap-1.5">
            <span className="flex h-4 w-4 items-center justify-center rounded bg-blue-maat text-[7px] font-bold text-white">M</span>
            <span className="font-heading text-[9px] font-bold text-text">MAAT</span>
          </div>
          <p className="mt-[18%] text-[8.5px] font-medium uppercase tracking-[0.14em] text-text-muted">Rapport RSE · Norme volontaire</p>
          <p className="mt-2 font-heading text-[22px] font-bold leading-[1.05] tracking-tight text-text">Ateliers Lemoine</p>
          <p className="mt-1 text-[9.5px] text-text-muted">{sector.label} · Diagnostic du 26 septembre 2026</p>
          <div className="mt-[12%] flex items-end gap-3 border-t border-border pt-4">
            <span className="numeric font-heading text-[40px] font-bold leading-none tracking-tight text-text">{score}</span>
            <span className="pb-1 text-[9.5px] text-text-muted">/ 100 · {getScoreLabel(score)}</span>
          </div>
          <ul className="mt-5 flex flex-col gap-1.5">
            {DOMAINS.map((d) => (
              <li key={d.key} className="grid grid-cols-[1fr_40%_1.25rem] items-center gap-2 text-[8.5px] text-text">
                <span className="truncate">{d.label}</span>
                <span className="h-1 rounded-full bg-bg">
                  <span className="block h-full rounded-full" style={{ width: `${DEMO_DOMAIN_SCORES[d.key]}%`, background: d.color }} />
                </span>
                <span className="numeric text-right">{DEMO_DOMAIN_SCORES[d.key]}</span>
              </li>
            ))}
          </ul>
          <p className="mt-auto flex items-center gap-1 text-[8px] text-text-muted">
            <Check size={9} /> Généré à la demande, jamais conservé sur nos serveurs
          </p>
        </div>
      </div>
    </div>
  )
}
