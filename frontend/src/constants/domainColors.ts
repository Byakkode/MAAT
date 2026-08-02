import type { RseDomain } from '../types/questionnaire'

// docs/specs/dashboard.md, section 3 : une couleur par domaine RSE, à ne jamais réattribuer.
// Valeurs définies une seule fois dans src/index.css (@theme, tokens partagés avec le reste
// de l'application) — jamais dupliquées ici en valeur hexadécimale.
export const DOMAIN_COLORS: Record<RseDomain, string> = {
  Environmental: 'var(--color-chart-environnement)',
  Social: 'var(--color-chart-social)',
  Ethics: 'var(--color-chart-ethique)',
  Procurement: 'var(--color-chart-achats)',
  Governance: 'var(--color-chart-gouvernance)',
}
