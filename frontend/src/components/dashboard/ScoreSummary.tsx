import { getScoreLabel, roundScoreForDisplay } from '../../constants/scoreLabels'

interface ScoreSummaryProps {
  score: number
  sectorCode: string
}

// docs/specs/dashboard.md, section 2. « Ne jamais présenter le score comme une note ou un
// classement » : aucune couleur d'alerte (rouge/orange) liée à la valeur elle-même, quelle
// que soit la tranche — le vocabulaire seul (SCORE_LABELS) porte la nuance.
export function ScoreSummary({ score, sectorCode }: ScoreSummaryProps) {
  const rounded = roundScoreForDisplay(score)
  const label = getScoreLabel(rounded)

  return (
    <section aria-labelledby="score-summary-heading" className="rounded-card border border-border bg-white p-5 shadow-card">
      <h2 id="score-summary-heading" className="mb-2 text-lg font-semibold text-text">
        Votre score RSE
      </h2>
      <p>
        <span className="text-4xl font-bold text-blue-maat tabular-nums lining-nums">{rounded}</span>
        <span className="text-lg text-text-muted"> / 100</span>
      </p>
      <p className="text-lg font-medium text-text">{label}</p>
      <p className="mt-2 text-sm text-text-muted">
        Secteur d&apos;activité : {sectorCode} — pondération appliquée à ce score.
      </p>
    </section>
  )
}
