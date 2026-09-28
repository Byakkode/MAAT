import * as DropdownMenu from '@radix-ui/react-dropdown-menu'
import { Check, ChevronDown } from 'lucide-react'
import type { ActionItemStatus } from '../../api/actionPlanApi'
import { ACTION_STATUS_LABELS } from '../../constants/actionStatusLabels'

const STATUS_ORDER: ActionItemStatus[] = ['Planned', 'InProgress', 'Blocked', 'Done']

const STATUS_CLASSES: Record<ActionItemStatus, string> = {
  Planned: 'border-border bg-bg text-text-muted',
  InProgress: 'border-blue-maat/40 bg-blue-maat/10 text-blue-maat-text',
  Blocked: 'border-orange/40 bg-orange/10 text-orange',
  Done: 'border-green-maat/40 bg-green-maat/10 text-green-maat-text',
}

const BADGE = 'inline-flex items-center gap-1 rounded-full border px-2.5 py-[3px] text-[11.5px] font-semibold'

interface ActionStatusMenuProps {
  status: ActionItemStatus
  // false : étiquette seule, sans menu (Starter, ou rôle Viewer).
  canEdit: boolean
  disabled?: boolean
  onChange: (status: ActionItemStatus) => void
}

// docs/specs/recommandations.md, section 4 bis : un clic sur l'étiquette ouvre les quatre
// statuts, et le statut choisi s'enregistre aussitôt — une seule ligne d'historique pour
// aller de Planifié à Terminé, là où l'ancien clic cyclique en laissait trois (Planifié →
// En cours → Bloqué → Terminé). Menu Radix : clavier, focus, Échap et clic extérieur gérés.
export function ActionStatusMenu({ status, canEdit, disabled = false, onChange }: ActionStatusMenuProps) {
  const label = ACTION_STATUS_LABELS[status]

  if (!canEdit) {
    return (
      <span className={`${BADGE} ${STATUS_CLASSES[status]}`}>
        <span className="sr-only">Statut : </span>
        {label}
      </span>
    )
  }

  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger
        disabled={disabled}
        aria-label={`Statut : ${label}. Changer le statut`}
        className={`${BADGE} ${STATUS_CLASSES[status]} cursor-pointer transition-opacity hover:opacity-80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-maat/40 disabled:cursor-default disabled:opacity-50`}
      >
        {label}
        <ChevronDown size={12} strokeWidth={2.5} aria-hidden />
      </DropdownMenu.Trigger>

      <DropdownMenu.Portal>
        <DropdownMenu.Content
          align="start"
          sideOffset={6}
          className="z-50 min-w-[10rem] rounded-xl border border-border bg-white p-1 shadow-card"
        >
          <DropdownMenu.RadioGroup
            value={status}
            onValueChange={(value) => {
              if (value !== status) onChange(value as ActionItemStatus)
            }}
          >
            {STATUS_ORDER.map((option) => (
              <DropdownMenu.RadioItem
                key={option}
                value={option}
                className="flex cursor-pointer items-center justify-between gap-3 rounded-lg px-2 py-1.5 outline-none data-[highlighted]:bg-bg"
              >
                <span className={`${BADGE} ${STATUS_CLASSES[option]}`}>{ACTION_STATUS_LABELS[option]}</span>
                <DropdownMenu.ItemIndicator>
                  <Check size={14} className="text-blue-maat" aria-hidden />
                </DropdownMenu.ItemIndicator>
              </DropdownMenu.RadioItem>
            ))}
          </DropdownMenu.RadioGroup>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}
