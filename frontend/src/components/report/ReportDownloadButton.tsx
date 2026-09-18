import { useEffect, useState } from 'react'
import { ApiError } from '../../api/authApi'
import * as accountApi from '../../api/accountApi'
import * as reportApi from '../../api/reportApi'
import { Button } from '../ui/Button'

interface ReportDownloadButtonProps {
  diagnosticId: string
}

type VerificationStatus = 'checking' | 'verified' | 'unverified'
type DownloadStatus = 'idle' | 'downloading' | 'error'

// docs/specs/rapport-pdf.md, section 6. La génération prend plusieurs secondes : le bouton
// affiche un état de chargement explicite et se désactive pendant l'opération, sinon
// l'utilisateur clique plusieurs fois et lance plusieurs générations. Un compte non vérifié
// voit le bouton, mais désactivé, avec un message expliquant la démarche — jamais masqué,
// ce qui laisserait croire que la fonctionnalité n'existe pas.
export function ReportDownloadButton({ diagnosticId }: ReportDownloadButtonProps) {
  const [verification, setVerification] = useState<VerificationStatus>('checking')
  const [downloadStatus, setDownloadStatus] = useState<DownloadStatus>('idle')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    accountApi
      .getCurrentUser()
      .then((user) => {
        if (!cancelled) {
          setVerification(user.emailVerified ? 'verified' : 'unverified')
        }
      })
      .catch(() => {
        // Repli optimiste : si le profil ne peut pas être récupéré, le bouton reste actif et
        // un éventuel 403 réel au clic sera traité par handleDownload ci-dessous.
        if (!cancelled) {
          setVerification('verified')
        }
      })
    return () => {
      cancelled = true
    }
  }, [])

  async function handleDownload() {
    setDownloadStatus('downloading')
    setError(null)
    try {
      await reportApi.downloadReport(diagnosticId)
      setDownloadStatus('idle')
    } catch (err) {
      if (err instanceof ApiError && err.status === 403) {
        setVerification('unverified')
      }
      setError(err instanceof Error ? err.message : 'Impossible de générer le rapport.')
      setDownloadStatus('error')
    }
  }

  const disabled = verification === 'unverified' || downloadStatus === 'downloading'

  return (
    <div className="flex flex-col items-start gap-1">
      <Button
        type="button"
        variant="secondary"
        size="sm"
        onClick={() => void handleDownload()}
        disabled={disabled}
        isLoading={downloadStatus === 'downloading'}
        aria-busy={downloadStatus === 'downloading'}
      >
        {downloadStatus === 'downloading' ? 'Génération en cours…' : 'Télécharger le rapport PDF'}
      </Button>
      {verification === 'unverified' && (
        <p className="text-xs text-text-muted">
          Votre adresse e-mail doit être vérifiée avant de pouvoir télécharger le rapport : consultez le
          message envoyé lors de votre inscription.
        </p>
      )}
      {downloadStatus === 'error' && verification !== 'unverified' && (
        <p role="alert" className="text-xs text-red">
          {error}
        </p>
      )}
    </div>
  )
}
