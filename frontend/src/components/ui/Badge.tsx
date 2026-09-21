import type { ReactNode } from 'react'

export type BadgeVariant = 'default' | 'blue' | 'green' | 'amber' | 'red' | 'orange'

/* Clés = valeurs de RseDomain (MAAT.Domain.Enums) sérialisées en string.
   Paires bg/text choisies pour respecter le contraste WCAG AA sur fond blanc :
   les tokens chart/* étant des couleurs vives, le fond est réduit à 10-15 % d'opacité
   et le texte utilise une variante sombre de la même teinte quand le token chart/*
   ne passe pas 4,5:1 seul (Environmental, Governance, Procurement). */
const DOMAIN_CLASSES: Record<string, string> = {
  Environmental: 'bg-green-maat/10 text-green-maat-text',
  Social: 'bg-chart-social/10 text-chart-social',
  Ethics: 'bg-chart-ethique/10 text-chart-ethique',
  Governance: 'bg-chart-gouvernance/10 text-blue-maat-text',
  Procurement: 'bg-chart-achats/15 text-text',
}

const VARIANT_CLASSES: Record<BadgeVariant, string> = {
  default: 'bg-border/60 text-text-muted',
  blue: 'bg-blue-maat/10 text-blue-maat-text',
  green: 'bg-green-maat/15 text-green-maat-text',
  amber: 'bg-amber/10 text-amber',
  red: 'bg-red/10 text-red',
  orange: 'bg-orange/15 text-text',
}

interface BadgeProps {
  children: ReactNode
  variant?: BadgeVariant
  /** Valeur de RseDomain (ex. "Environmental") — applique la couleur de domaine RSE. */
  domain?: string
  className?: string
}

export function Badge({ children, variant = 'default', domain, className = '' }: BadgeProps) {
  const colorClasses = domain
    ? (DOMAIN_CLASSES[domain] ?? VARIANT_CLASSES.default)
    : VARIANT_CLASSES[variant]

  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${colorClasses} ${className}`}
    >
      {children}
    </span>
  )
}
