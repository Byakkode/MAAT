import { Check } from 'lucide-react'
import { type KeyboardEvent, useEffect, useId, useRef, useState } from 'react'

// Champ texte libre avec suggestions (forme juridique…), à la place de <datalist> dont la
// liste, dessinée par le navigateur, ne suit pas la charte. Même liste que le choix du code NAF
// (NafCombobox) : carte blanche arrondie, option active surlignée. Motif ARIA « combobox » :
// flèches pour parcourir, Entrée pour choisir, Échap pour fermer ; la saisie reste libre, une
// suggestion n'est qu'un raccourci.

interface SuggestionInputProps {
  id: string
  value: string | null
  suggestions: string[]
  onChange: (value: string | null) => void
  readOnly?: boolean
  placeholder?: string
  maxLength?: number
  className: string
}

export function SuggestionInput({ id, value, suggestions, onChange, readOnly = false, placeholder, maxLength, className }: SuggestionInputProps) {
  const listboxId = useId()
  const [open, setOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(-1)
  const wrapperRef = useRef<HTMLDivElement>(null)

  const query = (value ?? '').trim().toLowerCase()
  // Toutes les suggestions quand le champ est vide ou vaut déjà l'une d'elles (on veut pouvoir
  // en changer), sinon celles qui contiennent la saisie.
  const exact = suggestions.some((s) => s.toLowerCase() === query)
  const matches = query === '' || exact ? suggestions : suggestions.filter((s) => s.toLowerCase().includes(query))
  const visible = open && !readOnly && matches.length > 0

  useEffect(() => {
    function handlePointerDown(e: PointerEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('pointerdown', handlePointerDown)
    return () => document.removeEventListener('pointerdown', handlePointerDown)
  }, [])

  function choose(suggestion: string) {
    onChange(suggestion)
    setOpen(false)
    setActiveIndex(-1)
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (readOnly) return
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setOpen(true)
      setActiveIndex((i) => Math.min(i + 1, matches.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActiveIndex((i) => Math.max(i - 1, 0))
    } else if (e.key === 'Enter' && visible && activeIndex >= 0) {
      e.preventDefault()
      choose(matches[activeIndex])
    } else if (e.key === 'Escape' || e.key === 'Tab') {
      setOpen(false)
    }
  }

  return (
    <div ref={wrapperRef} className="relative">
      <input
        id={id}
        type="text"
        role="combobox"
        autoComplete="off"
        aria-autocomplete="list"
        aria-expanded={visible}
        aria-controls={listboxId}
        aria-activedescendant={visible && activeIndex >= 0 ? `${listboxId}-${activeIndex}` : undefined}
        value={value ?? ''}
        placeholder={placeholder}
        maxLength={maxLength}
        readOnly={readOnly}
        onFocus={() => setOpen(true)}
        onChange={(e) => {
          onChange(e.target.value === '' ? null : e.target.value)
          setOpen(true)
          setActiveIndex(-1)
        }}
        onKeyDown={handleKeyDown}
        className={className}
      />
      {visible && (
        <ul
          id={listboxId}
          role="listbox"
          aria-label="Suggestions"
          className="absolute left-0 top-full z-50 mt-1.5 max-h-60 w-full overflow-y-auto rounded-xl border border-border bg-white p-1 shadow-popover"
        >
          {matches.map((suggestion, index) => {
            const selected = suggestion.toLowerCase() === query
            return (
              <li
                key={suggestion}
                id={`${listboxId}-${index}`}
                role="option"
                aria-selected={selected}
                // pointerdown avant le blur du champ : la liste ne se ferme pas avant le choix.
                onPointerDown={(e) => {
                  e.preventDefault()
                  choose(suggestion)
                }}
                className={`flex cursor-pointer items-center justify-between gap-3 rounded-lg px-3 py-2 text-[13.5px] ${
                  index === activeIndex ? 'bg-bg' : 'hover:bg-bg'
                } ${selected ? 'font-medium text-blue-maat-text' : 'text-text'}`}
              >
                {suggestion}
                {selected && <Check className="h-4 w-4 text-blue-maat" aria-hidden />}
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
