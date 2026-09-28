import { History } from 'lucide-react'
import { useEffect, useState } from 'react'
import { type ActionItemChange, getActionItemHistory } from '../../api/actionPlanApi'
import { describeChange } from './describeChange'

interface ActionItemHistoryProps {
  diagnosticId: string
  code: string
  // Change à chaque enregistrement du suivi : l'historique ouvert se recharge alors, pour
  // montrer la modification qu'on vient de faire.
  refreshKey: string | null
}

type Load = { status: 'idle' } | { status: 'loading' } | { status: 'error' } | { status: 'loaded'; changes: ActionItemChange[] }

const dateTimeFormat = new Intl.DateTimeFormat('fr-FR', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })

// Chargé à l'ouverture seulement : quarante-cinq actions ne déclenchent pas quarante-cinq
// appels à l'affichage de la page.
export function ActionItemHistory({ diagnosticId, code, refreshKey }: ActionItemHistoryProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [load, setLoad] = useState<Load>({ status: 'idle' })

  useEffect(() => {
    if (!isOpen) return
    let cancelled = false
    setLoad((current) => (current.status === 'loaded' ? current : { status: 'loading' }))
    getActionItemHistory(diagnosticId, code)
      .then((changes) => {
        if (!cancelled) setLoad({ status: 'loaded', changes })
      })
      .catch(() => {
        if (!cancelled) setLoad({ status: 'error' })
      })
    return () => {
      cancelled = true
    }
  }, [isOpen, diagnosticId, code, refreshKey])

  const listId = `history-${code}`

  return (
    <div className="mt-3 border-t border-border pt-3">
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        aria-expanded={isOpen}
        aria-controls={listId}
        className="flex items-center gap-1.5 text-[12px] font-semibold text-text-muted transition-colors hover:text-blue-maat-text"
      >
        <History size={13} aria-hidden />
        {isOpen ? "Masquer l'historique" : "Voir l'historique"}
      </button>

      {isOpen && (
        <div id={listId} className="mt-2">
          {load.status === 'loading' && (
            <p role="status" className="text-[12px] text-text-muted">
              Chargement…
            </p>
          )}
          {load.status === 'error' && (
            <p role="alert" className="text-[12px] text-red">
              Impossible de charger l&apos;historique.
            </p>
          )}
          {load.status === 'loaded' && load.changes.length === 0 && (
            <p className="text-[12px] text-text-muted">Aucune modification enregistrée pour cette action.</p>
          )}
          {load.status === 'loaded' && load.changes.length > 0 && (
            <ol aria-label="Historique du suivi" className="flex flex-col gap-1.5">
              {load.changes.map((change, index) => (
                <li key={`${change.changedAt}-${change.field}-${index}`} className="text-[12px] leading-snug">
                  <span className="text-text">{describeChange(change)}</span>
                  <span className="block text-[11px] text-text-muted">
                    <time dateTime={change.changedAt}>{dateTimeFormat.format(new Date(change.changedAt))}</time>
                    {' · '}
                    {change.changedBy ?? 'Compte supprimé'}
                  </span>
                </li>
              ))}
            </ol>
          )}
        </div>
      )}
    </div>
  )
}
