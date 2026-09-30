import { CircleCheck, CircleDashed, CircleMinus } from 'lucide-react'
import type { DisclosureState, VsmeCompleteness, VsmeDisclosure } from '../../api/vsmeApi'
import { VSME_GROUPS } from '../../constants/vsme'
import { Card } from '../ui/Card'

// Intitulés courts pour le sommaire, où l'intitulé complet de la norme passerait sur trois lignes.
const SHORT_LABELS: Record<VsmeDisclosure, string> = {
  B1: 'Base du rapport',
  B2: 'Pratiques et politiques',
  B3: 'Énergie et GES',
  B4: 'Pollution',
  B5: 'Biodiversité',
  B6: 'Eau',
  B7: 'Ressources et déchets',
  B8: 'Effectifs',
  B9: 'Santé et sécurité',
  B10: 'Rémunération et formation',
  B11: 'Corruption',
}

const STATE_ICON: Record<DisclosureState, { icon: typeof CircleCheck; className: string; label: string }> = {
  Complete: { icon: CircleCheck, className: 'text-green-maat', label: 'complète' },
  Incomplete: { icon: CircleDashed, className: 'text-orange', label: 'à compléter' },
  Omitted: { icon: CircleMinus, className: 'text-text-muted', label: 'omise' },
}

function ProgressRing({ value, total }: { value: number; total: number }) {
  const radius = 26
  const circumference = 2 * Math.PI * radius
  const complete = value === total
  return (
    <svg viewBox="0 0 64 64" className="h-16 w-16 shrink-0 -rotate-90" aria-hidden>
      <circle cx="32" cy="32" r={radius} fill="none" strokeWidth="6" className="stroke-border" />
      <circle
        cx="32"
        cy="32"
        r={radius}
        fill="none"
        strokeWidth="6"
        strokeLinecap="round"
        strokeDasharray={circumference}
        strokeDashoffset={circumference * (1 - value / total)}
        className={`transition-[stroke-dashoffset] duration-500 ${complete ? 'stroke-green-maat' : 'stroke-blue-maat'}`}
      />
    </svg>
  )
}

// Même complétude que la section 04 du rapport (VsmeCompleteness, côté serveur).
export function VsmeProgress({
  completeness,
  year,
  onSelect,
}: {
  completeness: VsmeCompleteness
  year: number
  onSelect: (code: VsmeDisclosure) => void
}) {
  const total = completeness.disclosures.length
  const stateOf = (code: VsmeDisclosure) => completeness.disclosures.find((d) => d.disclosure === code)?.state ?? 'Incomplete'

  return (
    <Card variant="flat" className="p-5">
      <div role="status" className="flex items-center gap-4">
        <div className="relative">
          <ProgressRing value={completeness.completeCount} total={total} />
          <span className="absolute inset-0 flex items-center justify-center font-heading text-[15px] font-bold text-text">
            {completeness.completeCount}/{total}
          </span>
        </div>
        <div className="min-w-0">
          <p className="font-heading text-[14px] font-semibold text-text">Complétude {year}</p>
          <p className="sr-only">
            {completeness.completeCount} information{completeness.completeCount > 1 ? 's' : ''} sur {total} complète
            {completeness.completeCount > 1 ? 's' : ''} pour {year}
          </p>
          <p className={`mt-0.5 text-[12px] leading-snug ${completeness.isCompliant ? 'text-green-maat-text' : 'text-text-muted'}`}>
            {completeness.isCompliant ? 'Conformité au module de base déclarée dans le rapport.' : 'Rapport partiel tant que tout n’est pas complété.'}
          </p>
        </div>
      </div>

      {completeness.isMicro && (
        <p className="mt-3 rounded-lg bg-bg px-3 py-2 text-[12px] text-text-muted">
          10 salariés ou moins : l’énergie, les émissions, l’eau et les déchets sont facultatifs.
        </p>
      )}

      <nav aria-label="Informations de la norme" className="mt-4 hidden border-t border-border pt-3 lg:block">
        {VSME_GROUPS.map((group) => (
          <div key={group.id} className="mt-2 first:mt-0">
            <p className="px-2 text-[10.5px] font-semibold uppercase tracking-[0.08em] text-text-muted">{group.label}</p>
            <ul className="mt-1">
              {group.codes.map((code) => {
                const meta = STATE_ICON[stateOf(code)]
                const Icon = meta.icon
                return (
                  <li key={code}>
                    <button
                      type="button"
                      onClick={() => onSelect(code)}
                      className="flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-[12.5px] text-text transition-colors hover:bg-bg"
                    >
                      <Icon className={`h-4 w-4 shrink-0 ${meta.className}`} aria-hidden />
                      <span className="w-7 shrink-0 font-semibold text-text-muted">{code}</span>
                      <span className="min-w-0 flex-1 truncate">{SHORT_LABELS[code]}</span>
                      <span className="sr-only">, {meta.label}</span>
                    </button>
                  </li>
                )
              })}
            </ul>
          </div>
        ))}
      </nav>
    </Card>
  )
}
