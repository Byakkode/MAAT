import { motion } from 'framer-motion'
import { BarChart2, CheckCircle, ClipboardList, FileText, LifeBuoy, Settings, TrendingUp } from 'lucide-react'
import { LogoHorizontal } from '../../components/ui/Logo'
import { ScoreRing } from '../../components/dashboard/ScoreRing'
import { getScoreLabel } from '../../constants/scoreLabels'
import { DEMO_DOMAIN_SCORES, DOMAINS, SECTORS, weightedScore } from '../content'
import { EASE_OUT } from '../motion'

const NAV = [
  { group: 'Analyse', items: [{ label: 'Tableau de bord', icon: BarChart2, active: true }, { label: 'Diagnostic', icon: ClipboardList }, { label: 'Indicateurs', icon: TrendingUp }] },
  { group: 'Pilotage', items: [{ label: "Plan d’actions", icon: CheckCircle }, { label: 'Rapports', icon: FileText }] },
  { group: 'Compte', items: [{ label: 'Mon compte', icon: Settings }, { label: 'Support', icon: LifeBuoy }] },
]

const INDUSTRY = SECTORS[0]
const GLOBAL = weightedScore(DEMO_DOMAIN_SCORES, INDUSTRY.weights)

// Reproduction statique du tableau de bord (src/pages/DashboardPage.tsx) : même coquille,
// même anneau de score (ScoreRing réutilisé tel quel), mêmes couleurs de domaine. Montrer
// le vrai produit plutôt qu'une illustration générique est ce qui distingue une vitrine
// de SaaS crédible d'un gabarit.
export function DashboardMock() {
  const weakest = DOMAINS.reduce((a, b) => (DEMO_DOMAIN_SCORES[a.key] <= DEMO_DOMAIN_SCORES[b.key] ? a : b))
  const strongest = DOMAINS.reduce((a, b) => (DEMO_DOMAIN_SCORES[a.key] >= DEMO_DOMAIN_SCORES[b.key] ? a : b))

  return (
    <div className="flex overflow-hidden rounded-card border border-border bg-white text-left" aria-hidden="true">
      <aside className="hidden w-52 shrink-0 flex-col gap-6 bg-sidebar px-4 py-5 md:flex">
        <LogoHorizontal tone="dark" className="px-1" />
        {NAV.map(({ group, items }) => (
          <div key={group}>
            <p className="px-2 pb-1.5 text-[10px] font-medium uppercase tracking-[0.12em] text-white/35">{group}</p>
            {items.map(({ label, icon: Icon, active }) => (
              <p
                key={label}
                className={`flex items-center gap-2.5 rounded-md px-2 py-1.5 text-[12.5px] ${active ? 'bg-white/10 font-semibold text-white' : 'text-white/55'}`}
              >
                <Icon size={14} className={active ? 'text-blue-maat' : ''} />
                {label}
              </p>
            ))}
          </div>
        ))}
      </aside>

      <div className="min-w-0 flex-1 bg-bg">
        <div className="flex items-center justify-between border-b border-border bg-white px-5 py-3">
          <div>
            <p className="font-heading text-[15px] font-semibold text-text">Tableau de bord</p>
            <p className="text-[11.5px] text-text-muted">Ateliers Lemoine · {INDUSTRY.label} ({INDUSTRY.code})</p>
          </div>
          <span className="avatar-gradient flex h-7 w-7 items-center justify-center rounded-full text-[11px] font-semibold text-white">AL</span>
        </div>

        <div className="grid gap-3 p-4 sm:p-5 lg:grid-cols-[minmax(0,5fr)_minmax(0,7fr)]">
          <div className="flex items-center gap-5 rounded-card border border-border bg-white p-5">
            <ScoreRing score={GLOBAL} size={124} delay={0.5} />
            <div>
              <p className="text-[11.5px] text-text-muted">Score RSE global</p>
              <p className="mt-1 font-heading text-[17px] font-semibold leading-tight text-text">{getScoreLabel(GLOBAL)}</p>
              <p className="numeric mt-3 inline-flex items-center gap-1.5 rounded-md bg-kpi-green px-2 py-1 text-[11px] font-medium text-green-maat-text">
                +17 pts depuis le diagnostic précédent
              </p>
            </div>
          </div>

          <div className="rounded-card border border-border bg-white p-5">
            <p className="mb-3.5 text-[11.5px] text-text-muted">Score par domaine</p>
            <ul className="flex flex-col gap-2.5">
              {DOMAINS.map((d, i) => (
                <li key={d.key} className="grid grid-cols-[7.5rem_1fr_2rem] items-center gap-3 text-[12px] sm:grid-cols-[9.5rem_1fr_2rem]">
                  <span className="truncate text-text">{d.label}</span>
                  <span className="h-1.5 overflow-hidden rounded-full bg-bg">
                    <motion.span
                      className="block h-full origin-left rounded-full"
                      style={{ background: d.color, width: `${DEMO_DOMAIN_SCORES[d.key]}%` }}
                      initial={{ scaleX: 0 }}
                      animate={{ scaleX: 1 }}
                      transition={{ duration: 1.2, ease: EASE_OUT, delay: 0.7 + i * 0.08 }}
                    />
                  </span>
                  <span className="numeric text-right font-medium text-text">{DEMO_DOMAIN_SCORES[d.key]}</span>
                </li>
              ))}
            </ul>
          </div>

          <div className="grid gap-3 sm:grid-cols-3 lg:col-span-2">
            <MiniKpi tone="blue" label="Actions en cours" value="4 / 11" />
            <MiniKpi tone="green" label="Point fort" value={strongest.label} />
            <MiniKpi tone="amber" label="À surveiller" value={weakest.label} />
          </div>
        </div>
      </div>
    </div>
  )
}

const KPI_TONES = {
  blue: 'border-l-blue-maat bg-kpi-blue',
  green: 'border-l-green-maat bg-kpi-green',
  amber: 'border-l-orange bg-kpi-amber',
}

// Cartes KPI de la charte : bordure gauche colorée de 4 px et fond teinté très subtil.
function MiniKpi({ tone, label, value }: { tone: keyof typeof KPI_TONES; label: string; value: string }) {
  return (
    <div className={`rounded-card border border-border border-l-4 px-4 py-3 ${KPI_TONES[tone]}`}>
      <p className="text-[11px] text-text-muted">{label}</p>
      <p className="numeric mt-0.5 truncate font-heading text-[14px] font-semibold text-text">{value}</p>
    </div>
  )
}
