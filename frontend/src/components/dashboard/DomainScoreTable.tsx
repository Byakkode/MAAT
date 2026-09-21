import { DOMAIN_COLORS } from '../../constants/domainColors'
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
    <div>
      <table className="w-full border-collapse text-sm tabular-nums lining-nums">
      <caption className="mb-2 text-left text-xs text-text-muted">Détail des scores par domaine</caption>
        <thead>
          <tr className="border-b border-border">
            <th scope="col" className="pb-2 text-left text-xs font-medium uppercase tracking-wide text-text-muted">
              Domaine
            </th>
            <th scope="col" className="pb-2 text-right text-xs font-medium uppercase tracking-wide text-text-muted">
              Score
            </th>
            <th scope="col" className="pb-2 text-right text-xs font-medium uppercase tracking-wide text-text-muted">
              Poids
            </th>
            <th scope="col" className="pb-2 text-right text-xs font-medium uppercase tracking-wide text-text-muted">
              Reco.
            </th>
          </tr>
        </thead>
        <tbody>
          {DOMAIN_ORDER.map((domain) => {
            const entry = domainScores.find((d) => d.domain === domain)
            const score = entry ? Math.round(entry.score) : null

            return (
              <tr key={domain} className="group border-b border-border last:border-0 hover:bg-bg/60">
                <th scope="row" className="py-2.5 pr-3 text-left font-normal">
                  <div className="flex items-center gap-2">
                    <span
                      aria-hidden
                      className="h-2 w-2 shrink-0 rounded-full"
                      style={{ backgroundColor: DOMAIN_COLORS[domain] }}
                    />
                    <span className="text-text">{DOMAIN_LABELS[domain]}</span>
                  </div>
                </th>
                <td className="py-2.5 pr-3">
                  {score != null ? (
                    <div className="flex items-center justify-end gap-2">
                      <div className="h-1.5 w-14 overflow-hidden rounded-full bg-border">
                        <div
                          className="h-full rounded-full transition-all"
                          style={{
                            width: `${score}%`,
                            backgroundColor: DOMAIN_COLORS[domain],
                          }}
                        />
                      </div>
                      <span className="w-5 text-right text-text">{score}</span>
                    </div>
                  ) : (
                    <span className="text-right text-text-muted" />
                  )}
                </td>
                <td className="py-2.5 pr-3 text-right text-text-muted">
                  {entry ? `${Math.round(entry.sectorWeight * 100)} %` : ''}
                </td>
                <td className="py-2.5 text-right text-text-muted">
                  {entry ? entry.triggeredRecommendationCount : ''}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
