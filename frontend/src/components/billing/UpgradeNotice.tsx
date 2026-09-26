import { Lock } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import type { ApiPlan } from '../../api/billingApi'
import { Card } from '../ui/Card'

interface UpgradeNoticeProps {
  // Première offre qui inclut la fonctionnalité (docs/specs/abonnement.md, section 8).
  requiredPlan: Extract<ApiPlan, 'Essential' | 'Professional'>
  title: string
  children?: ReactNode
  // compact : ligne discrète dans une carte existante plutôt qu'une carte à part entière.
  compact?: boolean
}

// Ce que l'offre n'inclut pas est remplacé par ce bloc, jamais laissé vide ni affiché grisé
// sans explication : l'utilisateur doit savoir pourquoi il ne le voit pas et comment l'obtenir.
// Le lien mène à l'écran des offres ; seul un Admin peut y changer d'offre, les autres membres
// y lisent un message qui les renvoie vers lui.
export function UpgradeNotice({ requiredPlan, title, children, compact = false }: UpgradeNoticeProps) {
  const content = (
    <div className="flex items-start gap-3">
      <Lock className="mt-0.5 h-4 w-4 shrink-0 text-text-muted" aria-hidden />
      <div className="min-w-0">
        <p className="text-sm font-semibold text-text">{title}</p>
        {children && <div className="mt-1 text-sm text-text-muted">{children}</div>}
        <p className="mt-1 text-sm text-text-muted">
          Inclus à partir de l&apos;offre {requiredPlan}.{' '}
          <Link to="/abonnement" className="font-medium text-blue-maat-text hover:underline">
            Voir les offres
          </Link>
        </p>
      </div>
    </div>
  )

  if (compact) {
    return <div className="rounded-lg border border-border bg-bg px-3 py-2.5">{content}</div>
  }

  return <Card as="section">{content}</Card>
}
