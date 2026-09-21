import { CheckCircle } from ‘lucide-react’

const FEATURES = [
  ‘45 questions, diagnostic en moins de 30 min’,
  "Plan d’actions personnalisé par domaine RSE",
  ‘Rapport PDF conforme au standard VSME’,
]

export function AuthPanel() {
  return (
    <aside
      className="hidden lg:flex lg:w-[40%] flex-col justify-between bg-sidebar p-10 text-white"
      aria-label="Présentation de MAAT"
    >
      <div>
        {/* Wordmark — cohérent avec la sidebar de l’AppShell */}
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-blue-maat">
            <span className="text-sm font-bold text-white">M</span>
          </div>
          <div>
            <p className="font-heading text-lg font-bold leading-tight text-white">MAAT</p>
            <p className="text-xs text-white/50">Diagnostic RSE pour PME</p>
          </div>
        </div>

        {/* Tagline */}
        <p className="mt-10 text-xl font-semibold leading-snug text-white">
          Mesurez, pilotez et améliorez votre performance RSE — en moins de 30&nbsp;min.
        </p>

        {/* Features */}
        <ul className="mt-8 flex flex-col gap-4">
          {FEATURES.map((feature) => (
            <li key={feature} className="flex items-start gap-3">
              <CheckCircle
                size={18}
                className="mt-0.5 shrink-0 text-green-maat"
                aria-hidden="true"
              />
              <span className="text-sm leading-relaxed text-white/75">{feature}</span>
            </li>
          ))}
        </ul>
      </div>

      {/* Footer */}
      <p className="text-xs text-white/40">Hébergé en France · Données souveraines</p>
    </aside>
  )
}
