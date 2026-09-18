import { CheckCircle } from 'lucide-react'

const FEATURES = [
  '45 questions, diagnostic en moins de 30 min',
  'Plan d’actions personnalisé par domaine RSE',
  'Rapport PDF conforme au standard VSME',
]

export function AuthPanel() {
  return (
    <aside
      className="hidden lg:flex lg:w-[40%] flex-col justify-between bg-blue-maat p-10 text-white"
      aria-label="Présentation de MAAT"
    >
      <div>
        {/* Wordmark */}
        <p className="text-2xl font-bold tracking-tight">MAAT</p>
        <p className="mt-1 text-sm text-white/70">Diagnostic RSE pour PME</p>

        {/* Tagline */}
        <p className="mt-10 text-xl font-semibold leading-snug">
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
              <span className="text-sm leading-relaxed text-white/90">{feature}</span>
            </li>
          ))}
        </ul>
      </div>

      {/* Footer */}
      <p className="text-xs text-white/50">Hébergé en France · Données souveraines</p>
    </aside>
  )
}
