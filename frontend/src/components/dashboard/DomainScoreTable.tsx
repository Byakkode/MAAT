import { DOMAIN_LABELS, DOMAIN_ORDER } from '../../types/questionnaire'
import type { DomainScore } from '../../types/dashboard'

interface DomainScoreTableProps {
  domainScores: DomainScore[]
}

// docs/specs/dashboard.md, section 3, cas 16 : alternative au radar pour les lecteurs
// d'écran — un <table> natif est présent dans le DOM (pas seulement révélé au survol) et
// accessible au clavier sans ARIA supplémentaire ; visuellement discret mais jamais masqué
// (ni sr-only, ni aria-hidden), contrairement au conteneur du graphique lui-même.
export function DomainScoreTable({ domainScores }: DomainScoreTableProps) {
  return (
    <table className="w-full border-collapse text-sm tabular-nums lining-nums">
      <caption className="mb-2 text-left text-xs text-text-muted">Détail des cinq scores par domaine</caption>
      <thead>
        <tr className="border-b border-border text-left text-text-muted">
          <th scope="col" className="py-1 pr-2 font-medium">
            Domaine
          </th>
          <th scope="col" className="py-1 pr-2 font-medium">
            Score
          </th>
          <th scope="col" className="py-1 pr-2 font-medium">
            Pondération sectorielle
          </th>
          <th scope="col" className="py-1 font-medium">
            Recommandations déclenchées
          </th>
        </tr>
      </thead>
      <tbody>
        {DOMAIN_ORDER.map((domain) => {
          const entry = domainScores.find((d) => d.domain === domain)
          return (
            <tr key={domain} className="border-b border-border last:border-0">
              <th scope="row" className="py-1 pr-2 text-left font-normal text-text">
                {DOMAIN_LABELS[domain]}
              </th>
              <td className="py-1 pr-2 text-text">{entry ? Math.round(entry.score) : '—'}</td>
              <td className="py-1 pr-2 text-text">{entry ? `${Math.round(entry.sectorWeight * 100)} %` : '—'}</td>
              <td className="py-1 text-text">{entry ? entry.triggeredRecommendationCount : '—'}</td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}
