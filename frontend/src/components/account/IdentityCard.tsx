import { ROLE_LABELS } from '../../constants/roleLabels'
import { useAuthStore } from '../../store/authStore'
import { useCurrentUserStore } from '../../store/currentUserStore'

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

  return (
    <section aria-labelledby="identity-heading" className="rounded-card border border-border bg-white p-5 shadow-card">
      <h2 id="identity-heading" className="mb-3 text-base font-semibold text-text">
        Identité
      </h2>
      <dl className="grid gap-3 text-sm">
        <div>
          <dt className="text-text-muted">Adresse e-mail</dt>
          <dd className="text-text">{email}</dd>
        </div>
        <div>
          <dt className="text-text-muted">Rôle</dt>
          <dd className="text-text">{role ? (ROLE_LABELS[role] ?? role) : ''}</dd>
        </div>
        <div>
          <dt className="text-text-muted">Entreprise</dt>
          <dd className="text-text">{companyName}</dd>
        </div>
        <div>
          <dt className="text-text-muted">Membre depuis</dt>
          <dd className="text-text tabular-nums lining-nums">{createdAt ? formatDate(createdAt) : ''}</dd>
        </div>
      </dl>
      <p className="mt-3 text-sm text-text-muted">L&apos;adresse e-mail n&apos;est pas modifiable pour le moment.</p>
    </section>
  )
}
