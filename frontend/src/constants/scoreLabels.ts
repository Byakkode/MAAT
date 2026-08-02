// docs/specs/dashboard.md, section 2. Constantes du frontend, réutilisées à l'identique dans
// le rapport PDF (génération QuestPDF, backend) : un même score ne doit jamais être qualifié
// différemment selon le support.
export interface ScoreLabelThreshold {
  min: number
  max: number
  label: string
}

export const SCORE_LABELS: readonly ScoreLabelThreshold[] = [
  { min: 0, max: 24, label: 'Démarche à initier' },
  { min: 25, max: 49, label: 'Premiers pas engagés' },
  { min: 50, max: 69, label: 'Démarche structurée' },
  { min: 70, max: 84, label: 'Démarche avancée' },
  { min: 85, max: 100, label: 'Démarche exemplaire' },
]

// Réplique ScoringService.RoundForDisplay (backend/MAAT.Domain/Services/ScoringService.cs) :
// arrondi au plus proche, .5 vers le haut. Équivalent à Math.Round(_, MidpointRounding.
// AwayFromZero) côté C# uniquement parce qu'un score n'est jamais négatif — ne pas réutiliser
// tel quel pour une valeur qui pourrait l'être.
export function roundScoreForDisplay(score: number): number {
  return Math.round(score)
}

export function getScoreLabel(roundedScore: number): string {
  const threshold = SCORE_LABELS.find((t) => roundedScore >= t.min && roundedScore <= t.max)
  return threshold?.label ?? SCORE_LABELS[SCORE_LABELS.length - 1].label
}
