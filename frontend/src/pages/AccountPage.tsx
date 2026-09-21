import { useEffect } from 'react'
import { DataExportCard } from '../components/account/DataExportCard'
import { DeleteAccountCard } from '../components/account/DeleteAccountCard'
import { IdentityCard } from '../components/account/IdentityCard'
import { PasswordChangeCard } from '../components/account/PasswordChangeCard'
import { PageHeader } from '../components/ui/PageHeader'
import { useCurrentUserStore } from '../store/currentUserStore'

// docs/specs/coquille-et-compte.md, sections 5 et 6 : quatre blocs, dans l'ordre de risque
// croissant — identité (lecture seule), mot de passe, export, suppression.
export function AccountPage() {
  const status = useCurrentUserStore((s) => s.status)
  const load = useCurrentUserStore((s) => s.load)

  useEffect(() => {
    if (status === 'idle') {
      void load()
    }
  }, [status, load])

  if (status === 'idle' || status === 'loading') {
    // section 7 : "Squelette de chargement plutôt qu'un indicateur centré, comme au tableau de
    // bord." DashboardPage affiche en réalité un simple texte de statut, pas un squelette : cet
    // écran suit donc le même patron que le reste de l'app plutôt que la description de la
    // spec, pour rester cohérent.
    return (
      <p role="status" className="text-text-muted">
        Chargement de votre compte…
      </p>
    )
  }

  if (status === 'error') {
    return (
      <p role="alert" className="text-red">
        Impossible de récupérer votre compte.
      </p>
    )
  }

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Mon compte" />
      {/* Deux colonnes sur les grands écrans : profil (lecture seule) à gauche, formulaires à droite */}
      <div className="grid gap-4 xl:grid-cols-[260px_1fr] xl:items-start">
        <IdentityCard />
        <div className="flex flex-col gap-4">
          <PasswordChangeCard />
          <DataExportCard />
          <DeleteAccountCard />
        </div>
      </div>
    </div>
  )
}
