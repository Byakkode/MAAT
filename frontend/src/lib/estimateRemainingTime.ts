// docs/specs/questionnaire.md, section 2 : l'estimation du temps restant se calcule sur la
// durée moyenne observée par question depuis le début du diagnostic, jamais sur une constante
// codée en dur.
export function estimateRemainingMinutes(
  startedAt: number,
  now: number,
  answeredCount: number,
  totalQuestions: number,
): number | null {
  if (answeredCount <= 0) {
    return null
  }
  const remaining = totalQuestions - answeredCount
  if (remaining <= 0) {
    return 0
  }
  const averageMsPerQuestion = (now - startedAt) / answeredCount
  return Math.max(1, Math.round((averageMsPerQuestion * remaining) / 60_000))
}
