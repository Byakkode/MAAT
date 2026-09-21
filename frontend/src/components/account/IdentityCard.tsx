import { ROLE_LABELS } from '../../constants/roleLabels'
import { useAuthStore } from '../../store/authStore'
import { useCurrentUserStore } from '../../store/currentUserStore'
import { Card } from '../ui/Card'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })
}

// docs/specs/coquille-et-compte.md, section 5 : "L'adresse n'est pas modifiable dans le MVP :
// la changer imposerait un nouveau cycle de vérification et une gestion de l'adresse
// intermédiaire." — pas de champ éditable, un simple constat.
export function IdentityCard() {
  const role = useAuthStore((s) => s.user?.role)
  const email = useCurrentUserStore((s) => s.email)
  const companyName = useCurrentUserStore((s) => s.companyName)
  const createdAt = useCurrentUserStore((s) => s.createdAt)

  const initial = email ? email[0].toUpperCase() : '?'
  const roleLabel = role ? (ROLE_LABELS[role] ?? role) : ''

  return (
    <Card as="section" aria-labelledby="identity-heading">
      {/* Avatar + identité principale */}
      <div className="mb-5 flex items-center gap-3">
        <div
          aria-hidden="true"
          className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-blue-maat/10 text-[1rem] font-bold text-blue-maat"
        >
          {initial}
        </div>
        <div className="min-w-0">
          <p className="truncate text-[13.5px] font-medium text-text">{email}</p>
          <p className="text-[12px] text-text-muted">{roleLabel}</p>
        </div>
      </div>

      <h2 id="identity-heading" className="mb-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-text-muted">
        Identité
      </h2>

      {/* Entreprise et date uniquement — email et rôle déjà affichés dans l'avatar ci-dessus */}
      <dl className="flex flex-col gap-2.5">
        <div className="flex items-baseline justify-between gap-3">
          <dt className="shrink-0 text-[12.5px] text-text-muted">Entreprise</dt>
          <dd className="truncate text-right text-[12.5px] font-medium text-text">{companyName}</dd>
        </div>
        <div className="flex items-baseline justify-between gap-3">
          <dt className="shrink-0 text-[12.5px] text-text-muted">Membre depuis</dt>
          <dd className="text-right text-[12.5px] font-medium tabular-nums lining-nums text-text">
            {createdAt ? formatDate(createdAt) : ''}
          </dd>
        </div>
      </dl>

      <p className="mt-4 border-t border-border pt-3 text-[11.5px] text-text-muted">
        L&apos;adresse e-mail n&apos;est pas modifiable pour le moment.
      </p>
    </Card>
  )
}
