import * as RadixSelect from '@radix-ui/react-select'
import { Check, ChevronDown } from 'lucide-react'

// Liste déroulante du site, à la place du <select> natif dont le menu, dessiné par le système,
// ne suit pas la charte (et diffère d'un navigateur à l'autre). Même habillage que le menu de
// statut du Plan d'actions (ActionStatusMenu) : carte blanche arrondie, ombre légère, option
// active surlignée, coche bleue sur la valeur choisie. Radix gère le clavier (flèches, saisie
// de la première lettre, Échap), le focus et l'annonce au lecteur d'écran.

// Même gabarit que les champs de saisie (h-10, rayon, ombre), pour aligner une liste et un
// champ texte posés côte à côte.
export const FIELD_TRIGGER_CLASS =
  'flex h-10 w-full items-center justify-between gap-2 rounded-input border border-border bg-white px-3 text-left text-[13.5px] text-text shadow-input transition-colors ' +
  'hover:border-border-strong focus:border-blue-maat focus:outline-none focus:ring-2 focus:ring-blue-maat/15 ' +
  'data-[placeholder]:text-text-muted/70 data-[state=open]:border-blue-maat data-[state=open]:ring-2 data-[state=open]:ring-blue-maat/15 ' +
  'disabled:cursor-default disabled:bg-bg disabled:shadow-none disabled:hover:border-border'

export interface SelectOption<T extends string> {
  value: T
  label: string
}

interface SelectProps<T extends string> {
  id?: string
  value: T | null
  options: SelectOption<T>[]
  onChange: (value: T) => void
  placeholder?: string
  disabled?: boolean
  // Sans <label> associé (liste dans une ligne de tableau, sélecteur d'exercice).
  'aria-label'?: string
  className?: string
}

export function Select<T extends string>({
  id,
  value,
  options,
  onChange,
  placeholder = 'Choisir…',
  disabled = false,
  'aria-label': ariaLabel,
  className = '',
}: SelectProps<T>) {
  return (
    // Radix réserve la chaîne vide à « aucune valeur » : null devient undefined, et le
    // placeholder s'affiche.
    <RadixSelect.Root value={value ?? undefined} onValueChange={(next) => onChange(next as T)} disabled={disabled}>
      <RadixSelect.Trigger id={id} aria-label={ariaLabel} className={`${FIELD_TRIGGER_CLASS} ${className}`}>
        <span className="min-w-0 truncate">
          <RadixSelect.Value placeholder={placeholder} />
        </span>
        <RadixSelect.Icon asChild>
          <ChevronDown className="h-4 w-4 shrink-0 text-text-muted" aria-hidden />
        </RadixSelect.Icon>
      </RadixSelect.Trigger>

      <RadixSelect.Portal>
        <RadixSelect.Content
          position="popper"
          sideOffset={6}
          className="z-50 max-h-[min(var(--radix-select-content-available-height),18rem)] min-w-[var(--radix-select-trigger-width)] overflow-hidden rounded-xl border border-border bg-white shadow-popover"
        >
          <RadixSelect.Viewport className="p-1">
            {options.map((option) => (
              <RadixSelect.Item
                key={option.value}
                value={option.value}
                className="relative flex cursor-pointer select-none items-center justify-between gap-3 rounded-lg py-2 pl-3 pr-2 text-[13.5px] text-text outline-none data-[highlighted]:bg-bg data-[state=checked]:font-medium data-[state=checked]:text-blue-maat-text"
              >
                <RadixSelect.ItemText>{option.label}</RadixSelect.ItemText>
                <RadixSelect.ItemIndicator>
                  <Check className="h-4 w-4 text-blue-maat" aria-hidden />
                </RadixSelect.ItemIndicator>
              </RadixSelect.Item>
            ))}
          </RadixSelect.Viewport>
        </RadixSelect.Content>
      </RadixSelect.Portal>
    </RadixSelect.Root>
  )
}
