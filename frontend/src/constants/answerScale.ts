// docs/specs/questionnaire.md, section 3 : la valeur stockée en base est un entier de 0 à 5,
// mais n'est jamais présentée comme un nombre — une échelle numérique abstraite produit des
// réponses de complaisance. Ces libellés relèvent de la présentation, pas du backend : une
// future traduction ne doit pas exiger de migration.
export interface AnswerScaleOption {
  value: number
  label: string
}

export const ANSWER_SCALE: readonly AnswerScaleOption[] = [
  { value: 0, label: "Non, ce n'est pas en place" },
  { value: 1, label: 'Nous y réfléchissons' },
  { value: 2, label: 'Une démarche a été initiée' },
  { value: 3, label: 'En cours de déploiement' },
  { value: 4, label: 'Largement déployé' },
  { value: 5, label: 'Pleinement en place et suivi' },
]
