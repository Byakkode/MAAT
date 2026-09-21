import { CheckCircle, ShieldCheck, FileText, BarChart2 } from 'lucide-react'

const FEATURES = [
  {
    icon: BarChart2,
    text: '45 questions, diagnostic en moins de 30 min',
  },
  {
    icon: CheckCircle,
    text: "Plan d'actions personnalisé par domaine RSE",
  },
  {
    icon: FileText,
    text: 'Rapport PDF conforme au standard VSME',
  },
  {
    icon: ShieldCheck,
    text: 'Données hébergées en France, souveraineté garantie',
  },
]

const STATS = [
  { value: '45', label: 'questions RSE' },
  { value: '5', label: 'domaines évalués' },
  { value: '< 30', label: 'minutes' },
]

export function AuthPanel() {
  return (
    <aside
      className="hidden lg:flex lg:w-[44%] flex-col justify-between overflow-hidden"
      style={{ background: '#0c1322' }}
      aria-label="Présentation de MAAT"
    >
      <div className="flex flex-col gap-10 p-10">
        {/* Wordmark */}
        <div className="flex items-center gap-3">
          <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-blue-maat">
            <span className="text-sm font-bold text-white">M</span>
          </div>
          <div>
            <p className="text-[15px] font-bold leading-tight tracking-tight text-white">MAAT</p>
            <p className="text-[11px] text-white/40">Diagnostic RSE pour PME</p>
          </div>
        </div>

        {/* Heading */}
        <div>
          <h2 className="text-[1.5rem] font-semibold leading-snug tracking-tight text-white">
            Mesurez et pilotez votre performance&nbsp;RSE en moins de 30&nbsp;min.
          </h2>
          <p className="mt-3 text-[13.5px] leading-relaxed text-white/50">
            Questionnaire de 45 questions, score pondéré par secteur NAF, recommandations
            personnalisées et rapport PDF conforme VSME.
          </p>
        </div>

        {/* Features */}
        <ul className="flex flex-col gap-3">
          {FEATURES.map(({ icon: Icon, text }) => (
            <li key={text} className="flex items-center gap-3">
              <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md bg-white/[0.07]">
                <Icon size={13} className="text-white/60" aria-hidden="true" />
              </div>
              <span className="text-[13px] text-white/60">{text}</span>
            </li>
          ))}
        </ul>

        {/* Stats row */}
        <div className="flex gap-8 border-t border-white/[0.07] pt-8">
          {STATS.map(({ value, label }) => (
            <div key={label}>
              <p className="text-2xl font-bold text-white tabular-nums">{value}</p>
              <p className="mt-0.5 text-[11px] text-white/40">{label}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Footer */}
      <div className="px-10 pb-8">
        <p className="text-[11px] text-white/25">Hébergé en France · Données souveraines · RGPD conforme</p>
      </div>
    </aside>
  )
}
