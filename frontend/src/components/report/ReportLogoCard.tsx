import { ImageIcon } from 'lucide-react'
import { type ChangeEvent, useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/authApi'
import {
  ACCEPTED_LOGO_TYPES,
  MAX_LOGO_BYTES,
  deleteCompanyLogo,
  fetchCompanyLogo,
  uploadCompanyLogo,
} from '../../api/companyLogoApi'
import { useEntitlements } from '../../billing/entitlements'
import { useAuthStore } from '../../store/authStore'
import { UpgradeNotice } from '../billing/UpgradeNotice'
import { Button } from '../ui/Button'
import { Card } from '../ui/Card'

type Busy = 'loading' | 'uploading' | 'deleting' | null

// docs/specs/rapport-pdf.md, section 7 : logo de l'entreprise sur la page de garde du rapport.
// L'aperçu reproduit le traitement du document — cartouche blanc sur le bandeau foncé — pour
// que l'administrateur voie ce que verra son lecteur, pas seulement le fichier envoyé.
export function ReportLogoCard() {
  const { canCustomizeReportLogo } = useEntitlements()
  const isAdmin = useAuthStore((state) => state.user?.role === 'Admin')
  const fileInput = useRef<HTMLInputElement>(null)

  const [logoUrl, setLogoUrl] = useState<string | null>(null)
  const [busy, setBusy] = useState<Busy>('loading')
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  // Un seul object URL vivant à la fois : l'ancien est libéré dès qu'il est remplacé.
  const showLogo = useCallback((blob: Blob | null) => {
    setLogoUrl((previous) => {
      if (previous) URL.revokeObjectURL(previous)
      return blob ? URL.createObjectURL(blob) : null
    })
  }, [])

  const reload = useCallback(async () => {
    showLogo(await fetchCompanyLogo())
  }, [showLogo])

  // Offre qui n'inclut pas le logo : rien à charger, la carte propose l'offre supérieure.
  useEffect(() => {
    if (!canCustomizeReportLogo) return
    let cancelled = false
    fetchCompanyLogo()
      .then((blob) => {
        if (!cancelled) showLogo(blob)
      })
      .catch(() => {
        if (!cancelled) setError('Impossible de charger le logo actuel.')
      })
      .finally(() => {
        if (!cancelled) setBusy(null)
      })
    return () => {
      cancelled = true
    }
  }, [canCustomizeReportLogo, showLogo])

  useEffect(
    () => () => {
      setLogoUrl((previous) => {
        if (previous) URL.revokeObjectURL(previous)
        return null
      })
    },
    [],
  )

  async function handleFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    // Permet de renvoyer le même fichier après une erreur : sans cela, onChange ne se
    // redéclenche pas.
    event.target.value = ''
    if (!file) return

    setError(null)
    setNotice(null)
    if (file.size > MAX_LOGO_BYTES) {
      setError('Le logo ne doit pas dépasser 2 Mo.')
      return
    }

    setBusy('uploading')
    try {
      await uploadCompanyLogo(file)
      await reload()
      setNotice('Logo enregistré. Il figurera sur vos prochains rapports.')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Impossible d'enregistrer le logo.")
    } finally {
      setBusy(null)
    }
  }

  async function handleDelete() {
    setError(null)
    setNotice(null)
    setBusy('deleting')
    try {
      await deleteCompanyLogo()
      showLogo(null)
      setNotice('Logo retiré.')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Impossible de retirer le logo.')
    } finally {
      setBusy(null)
    }
  }

  if (!canCustomizeReportLogo) {
    return (
      <UpgradeNotice requiredPlan="Essential" title="Votre logo sur le rapport">
        Ajoutez le logo de votre entreprise en page de garde du document que vous transmettez à vos clients et partenaires.
      </UpgradeNotice>
    )
  }

  return (
    <Card as="section" aria-labelledby="report-logo-heading" className="flex flex-col gap-4">
      <div>
        <h2 id="report-logo-heading" className="mb-1 flex items-center gap-2 text-base font-semibold text-text">
          <ImageIcon size={15} className="shrink-0 text-text-muted" aria-hidden="true" />
          Logo sur le rapport
        </h2>
        <p className="text-[13px] text-text-muted">
          Affiché en page de garde, à côté du nom de votre entreprise. PNG, JPEG ou WebP, 2 Mo au maximum ; un fond
          transparent donne le meilleur rendu.
        </p>
      </div>

      <div className="flex items-center gap-4 rounded-lg bg-sidebar px-5 py-4">
        <div className="flex h-20 w-44 shrink-0 items-center justify-center rounded-lg bg-white p-2.5">
          {busy === 'loading' ? (
            <p role="status" className="text-xs text-text-muted">
              Chargement…
            </p>
          ) : logoUrl ? (
            <img src={logoUrl} alt="Logo actuel de votre entreprise" className="max-h-full max-w-full object-contain" />
          ) : (
            <p className="text-center text-xs text-text-muted">Aucun logo</p>
          )}
        </div>
        <p className="text-[13px] text-white/80">Aperçu de la page de garde</p>
      </div>

      {isAdmin ? (
        <div className="flex flex-wrap gap-2">
          <input
            ref={fileInput}
            type="file"
            accept={ACCEPTED_LOGO_TYPES}
            aria-label="Fichier du logo"
            className="hidden"
            onChange={(e) => void handleFile(e)}
          />
          <Button
            type="button"
            size="sm"
            isLoading={busy === 'uploading'}
            disabled={busy !== null}
            onClick={() => fileInput.current?.click()}
          >
            {logoUrl ? 'Remplacer le logo' : 'Ajouter un logo'}
          </Button>
          {logoUrl && (
            <Button
              type="button"
              size="sm"
              variant="secondary"
              isLoading={busy === 'deleting'}
              disabled={busy !== null}
              onClick={() => void handleDelete()}
            >
              Retirer le logo
            </Button>
          )}
        </div>
      ) : (
        <p className="text-[13px] text-text-muted">Seul un administrateur de votre entreprise peut modifier le logo.</p>
      )}

      {error && (
        <p role="alert" className="text-[13px] text-red">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="text-[13px] text-green-maat-text">
          {notice}
        </p>
      )}
    </Card>
  )
}
