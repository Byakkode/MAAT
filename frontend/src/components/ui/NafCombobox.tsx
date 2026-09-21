import { useId, useRef, useState, useEffect, useCallback } from 'react'
import { NAF_CODES } from '../../constants/nafCodes'

interface NafComboboxProps {
  label: string
  value: string
  onChange: (code: string) => void
  required?: boolean
  error?: string
}

const MAX_RESULTS = 60

function formatSelected(code: string): string {
  const entry = NAF_CODES.find((n) => n.code === code)
  return entry ? `${entry.code} — ${entry.label}` : code
}

function filterCodes(query: string) {
  if (!query.trim()) return []
  // Chaque mot doit être présent dans le code ou le libellé (gère pluriels, accords)
  const words = query.toLowerCase().split(/\s+/).filter(Boolean)
  return NAF_CODES.filter((n) => {
    const haystack = `${n.code} ${n.label}`.toLowerCase()
    return words.every((w) => haystack.includes(w))
  }).slice(0, MAX_RESULTS)
}

export function NafCombobox({ label, value, onChange, required, error }: NafComboboxProps) {
  const inputId = useId()
  const listboxId = useId()

  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [activeIndex, setActiveIndex] = useState(-1)

  const wrapperRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLUListElement>(null)

  const results = filterCodes(query)

  // Ferme le dropdown en cliquant à l'extérieur
  useEffect(() => {
    function handlePointerDown(e: PointerEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('pointerdown', handlePointerDown)
    return () => document.removeEventListener('pointerdown', handlePointerDown)
  }, [])

  // Scrolle l'option active dans le viewport
  useEffect(() => {
    if (activeIndex < 0 || !listRef.current) return
    const item = listRef.current.children[activeIndex] as HTMLElement | undefined
    item?.scrollIntoView?.({ block: 'nearest' })
  }, [activeIndex])

  const selectOption = useCallback(
    (code: string) => {
      onChange(code)
      setOpen(false)
      setQuery('')
      setActiveIndex(-1)
    },
    [onChange],
  )

  function handleInputChange(e: React.ChangeEvent<HTMLInputElement>) {
    setQuery(e.target.value)
    setOpen(true)
    setActiveIndex(-1)
    // Si l'utilisateur efface, on désélectionne
    if (!e.target.value) onChange('')
  }

  function handleFocus() {
    setQuery('')
    setOpen(true)
    setActiveIndex(-1)
  }

  function handleBlur() {
    // Si rien n'est sélectionné et le champ est vide, on restore le dernier code valide
    // On ne ferme PAS ici : le pointerdown sur une option passe avant le blur
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (!open) {
      if (e.key === 'ArrowDown' || e.key === 'Enter') {
        setOpen(true)
        return
      }
      return
    }

    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault()
        setActiveIndex((i) => Math.min(i + 1, results.length - 1))
        break
      case 'ArrowUp':
        e.preventDefault()
        setActiveIndex((i) => Math.max(i - 1, -1))
        break
      case 'Enter':
        e.preventDefault()
        if (activeIndex >= 0 && results[activeIndex]) {
          selectOption(results[activeIndex].code)
        }
        break
      case 'Escape':
        setOpen(false)
        setActiveIndex(-1)
        break
      case 'Tab':
        setOpen(false)
        break
    }
  }

  const inputDisplayValue = open ? query : value ? formatSelected(value) : query

  const inputClass = [
    'w-full rounded-[10px] border bg-white px-3.5 py-2.5 text-sm text-text',
    'shadow-[0_1px_2px_rgba(0,0,0,0.04)]',
    'transition-all duration-150',
    'placeholder:text-text-muted/60',
    error
      ? 'border-red/50 focus:border-red focus:ring-2 focus:ring-red/10 focus:outline-none'
      : 'border-border focus:border-blue-maat/70 focus:ring-2 focus:ring-blue-maat/10 focus:outline-none',
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <div ref={wrapperRef} className="relative flex flex-col gap-1.5">
      <label htmlFor={inputId} className="text-[13px] font-medium text-text">
        {label}
      </label>

      <div
        role="combobox"
        aria-expanded={open && results.length > 0}
        aria-haspopup="listbox"
        aria-owns={listboxId}
      >
        <input
          ref={inputRef}
          id={inputId}
          type="text"
          autoComplete="off"
          role="combobox"
          aria-autocomplete="list"
          aria-controls={listboxId}
          aria-activedescendant={
            activeIndex >= 0 ? `${listboxId}-opt-${activeIndex}` : undefined
          }
          aria-required={required}
          value={inputDisplayValue}
          onChange={handleInputChange}
          onFocus={handleFocus}
          onBlur={handleBlur}
          onKeyDown={handleKeyDown}
          placeholder="Tapez le code ou l'activité…"
          className={inputClass}
        />
      </div>

      {open && results.length > 0 && (
        <ul
          ref={listRef}
          id={listboxId}
          role="listbox"
          aria-label="Codes NAF correspondants"
          className="absolute top-full left-0 z-50 mt-1 max-h-56 w-full min-w-[320px] overflow-y-auto rounded-[10px] border border-border bg-white py-1 shadow-[0_8px_24px_rgba(0,0,0,0.12)]"
        >
          {results.map((n, i) => (
            <li
              key={n.code}
              id={`${listboxId}-opt-${i}`}
              role="option"
              aria-selected={n.code === value}
              // pointerdown avant blur : pas de fermeture intempestive
              onPointerDown={(e) => {
                e.preventDefault()
                selectOption(n.code)
              }}
              className={[
                'flex cursor-pointer items-baseline gap-2.5 px-3.5 py-2 text-sm',
                i === activeIndex ? 'bg-blue-maat/8 text-text' : 'text-text hover:bg-bg',
                n.code === value ? 'font-medium' : '',
              ]
                .filter(Boolean)
                .join(' ')}
            >
              <span className="w-14 shrink-0 font-mono text-[12px] font-semibold text-blue-maat-text">
                {n.code}
              </span>
              <span className="truncate text-[12px] text-text-muted">{n.label}</span>
            </li>
          ))}
        </ul>
      )}

      {/* Indication si aucun résultat */}
      {open && query.trim() && results.length === 0 && (
        <div className="absolute top-full left-0 z-50 mt-1 w-full rounded-[10px] border border-border bg-white px-3.5 py-3 text-sm text-text-muted shadow-[0_8px_24px_rgba(0,0,0,0.12)]">
          Aucun code NAF trouvé pour « {query} »
        </div>
      )}

      {error && (
        <p role="alert" className="text-xs text-red">
          {error}
        </p>
      )}
    </div>
  )
}
