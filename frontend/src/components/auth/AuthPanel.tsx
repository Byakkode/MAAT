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
      className="relative hidden lg:flex lg:w-[42%] flex-col justify-between overflow-hidden"
      style={{
        background:
          'radial-gradient(ellipse at 20% 20%, rgba(21,101,255,0.55) 0%, transparent 50%),' +
          'radial-gradient(ellipse at 85% 80%, rgba(13,40,110,0.9) 0%, transparent 50%),' +
          '#0a1628',
      }}
      aria-label="Présentation de MAAT"
    >
      {/* Grille de points en overlay */}
      <div
        className="absolute inset-0 pointer-events-none"
        style={{
          backgroundImage: 'radial-gradient(circle, rgba(255,255,255,0.06) 1px, transparent 1px)',
          backgroundSize: '22px 22px',
        }}
        aria-hidden
      />

      <div className="relative flex flex-col gap-8 p-10">
        {/* Wordmark */}
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-blue-maat shadow-[0_0_18px_rgba(21,101,255,0.5)] ring-1 ring-blue-maat/60">
            <span className="text-sm font-bold text-white">M</span>
          </div>
          <div>
            <p className="font-heading text-lg font-bold leading-tight text-white">MAAT</p>
            <p className="text-xs text-white/45">Diagnostic RSE pour PME</p>
          </div>
        </div>

        {/* Tagline */}
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-widest text-blue-maat/80 mb-3">
            Plateforme RSE
          </p>
          <h2 className="text-[1.6rem] font-bold leading-snug tracking-tight text-white">
            Mesurez et pilotez votre performance&nbsp;RSE en moins de 30&nbsp;min.
          </h2>
        </div>

        {/* Features */}
        <ul className="flex flex-col gap-3.5">
          {FEATURES.map(({ icon: Icon, text }) => (
            <li key={text} className="flex items-start gap-3">
              <div className="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-md bg-blue-maat/20">
                <Icon size={12} className="text-blue-maat/90" aria-hidden="true" />
              </div>
              <span className="text-[13.5px] leading-relaxed text-white/70">{text}</span>
            </li>
          ))}
        </ul>

        {/* Stats row */}
        <div className="flex gap-6 border-t border-white/10 pt-6">
          {STATS.map(({ value, label }) => (
            <div key={label}>
              <p className="font-heading text-2xl font-bold text-white tabular-nums">{value}</p>
              <p className="mt-0.5 text-[11px] text-white/45">{label}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Footer */}
      <div className="relative px-10 pb-8">
        <p className="text-[11px] text-white/30">Hébergé en France · Données souveraines · RGPD conforme</p>
      </div>
    </aside>
  )
}
