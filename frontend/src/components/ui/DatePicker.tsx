import * as Popover from '@radix-ui/react-popover'
import { CalendarDays, ChevronLeft, ChevronRight, X } from 'lucide-react'
import { type KeyboardEvent, useEffect, useId, useRef, useState } from 'react'
import {
  addDays,
  addMonths,
  type CalendarDay,
  daysInMonth,
  formatLongDate,
  monthGrid,
  MONTHS,
  parseIsoDate,
  sameDay,
  toIsoDate,
  today,
  WEEKDAYS_LONG,
  WEEKDAYS_SHORT,
  weekdayIndex,
} from '../../lib/calendarDays'
import { FIELD_TRIGGER_CLASS } from './Select'

// Sélecteur de date du site, à la place de <input type="date"> dont le calendrier, dessiné par
// le navigateur, ne suit pas la charte. Valeur au format « aaaa-mm-jj », comme le champ natif
// qu'il remplace : les écrans et l'API ne changent pas. Grille écrite à la main, sans
// bibliothèque de dates : semaine commençant le lundi, noms des mois et des jours en français,
// jamais de fuseau horaire (un jour du calendrier reste ce jour-là, voir ActionItemProgress).
// Clavier : flèches (jour, semaine), Page précédente/suivante (mois), Début/Fin (semaine),
// Entrée pour choisir, Échap pour fermer (Radix).

function Calendar({ selected, onSelect }: { selected: CalendarDay | null; onSelect: (day: CalendarDay) => void }) {
  const [focused, setFocused] = useState<CalendarDay>(selected ?? today())
  const [view, setView] = useState<'days' | 'years'>('days')
  const gridRef = useRef<HTMLDivElement>(null)
  const now = today()

  // Le jour qui a le focus clavier le garde quand la grille change de mois.
  useEffect(() => {
    if (view !== 'days') return
    gridRef.current?.querySelector<HTMLButtonElement>('[data-focused="true"]')?.focus()
  }, [focused, view])

  function handleKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    const moves: Record<string, () => CalendarDay> = {
      ArrowLeft: () => addDays(focused, -1),
      ArrowRight: () => addDays(focused, 1),
      ArrowUp: () => addDays(focused, -7),
      ArrowDown: () => addDays(focused, 7),
      PageUp: () => addMonths(focused, -1),
      PageDown: () => addMonths(focused, 1),
      Home: () => addDays(focused, -weekdayIndex(focused)),
      End: () => addDays(focused, 6 - weekdayIndex(focused)),
    }
    const move = moves[e.key]
    if (!move) return
    e.preventDefault()
    setFocused(move())
  }

  if (view === 'years') {
    const firstYear = focused.year - (focused.year % 12)
    return (
      <div className="w-[17rem]">
        <div className="flex items-center justify-between">
          <NavButton label="Années précédentes" onClick={() => setFocused({ ...focused, year: focused.year - 12 })} icon={ChevronLeft} />
          <p className="font-heading text-[13.5px] font-semibold text-text">
            {firstYear} à {firstYear + 11}
          </p>
          <NavButton label="Années suivantes" onClick={() => setFocused({ ...focused, year: focused.year + 12 })} icon={ChevronRight} />
        </div>
        <div className="mt-3 grid grid-cols-4 gap-1">
          {Array.from({ length: 12 }, (_, i) => firstYear + i).map((year) => (
            <button
              key={year}
              type="button"
              onClick={() => {
                setFocused({ ...focused, year, day: Math.min(focused.day, daysInMonth(year, focused.month)) })
                setView('days')
              }}
              className={`rounded-lg py-2 text-[13px] tabular-nums transition-colors ${
                year === focused.year ? 'bg-blue-maat font-semibold text-white' : year === now.year ? 'font-semibold text-blue-maat-text hover:bg-bg' : 'text-text hover:bg-bg'
              }`}
            >
              {year}
            </button>
          ))}
        </div>
      </div>
    )
  }

  const grid = monthGrid(focused.year, focused.month)
  return (
    <div className="w-[17rem]">
      <div className="flex items-center justify-between">
        <NavButton label="Mois précédent" onClick={() => setFocused(addMonths(focused, -1))} icon={ChevronLeft} />
        <button
          type="button"
          onClick={() => setView('years')}
          aria-label={`${MONTHS[focused.month]} ${focused.year}, choisir l’année`}
          className="rounded-lg px-2 py-1 font-heading text-[13.5px] font-semibold capitalize text-text transition-colors hover:bg-bg"
        >
          {MONTHS[focused.month]} {focused.year}
        </button>
        <NavButton label="Mois suivant" onClick={() => setFocused(addMonths(focused, 1))} icon={ChevronRight} />
      </div>

      <div role="grid" aria-label={`${MONTHS[focused.month]} ${focused.year}`} ref={gridRef} onKeyDown={handleKeyDown} className="mt-3">
        <div role="row" className="grid grid-cols-7">
          {WEEKDAYS_SHORT.map((label, i) => (
            <span key={label} role="columnheader" aria-label={WEEKDAYS_LONG[i]} className="py-1 text-center text-[11px] font-semibold uppercase text-text-muted">
              {label}
            </span>
          ))}
        </div>
        {Array.from({ length: grid.length / 7 }, (_, week) => (
          <div key={week} role="row" className="grid grid-cols-7 gap-y-0.5">
            {grid.slice(week * 7, week * 7 + 7).map((day) => {
              const outside = day.month !== focused.month
              const isSelected = sameDay(day, selected)
              const isToday = sameDay(day, now)
              const isFocused = sameDay(day, focused)
              return (
                <span key={toIsoDate(day)} role="gridcell" aria-selected={isSelected}>
                  <button
                    type="button"
                    tabIndex={isFocused ? 0 : -1}
                    data-focused={isFocused}
                    aria-label={`${WEEKDAYS_LONG[weekdayIndex(day)]} ${formatLongDate(day)}`}
                    aria-current={isToday ? 'date' : undefined}
                    onClick={() => onSelect(day)}
                    onFocus={() => !isFocused && setFocused(day)}
                    className={`mx-auto flex h-8 w-8 items-center justify-center rounded-lg text-[13px] tabular-nums transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-maat/40 ${
                      isSelected
                        ? 'bg-blue-maat font-semibold text-white shadow-button-primary'
                        : isToday
                          ? 'font-semibold text-blue-maat-text ring-1 ring-blue-maat/40 hover:bg-blue-maat/10'
                          : outside
                            ? 'text-text-muted/50 hover:bg-bg'
                            : 'text-text hover:bg-bg'
                    }`}
                  >
                    {day.day}
                  </button>
                </span>
              )
            })}
          </div>
        ))}
      </div>
    </div>
  )
}

function NavButton({ label, onClick, icon: Icon }: { label: string; onClick: () => void; icon: typeof ChevronLeft }) {
  return (
    <button type="button" onClick={onClick} aria-label={label} className="rounded-lg p-1.5 text-text-muted transition-colors hover:bg-bg hover:text-text">
      <Icon className="h-4 w-4" aria-hidden />
    </button>
  )
}

interface DatePickerProps {
  id?: string
  value: string | null
  onChange: (value: string | null) => void
  placeholder?: string
  disabled?: boolean
  'aria-label'?: string
  className?: string
}

export function DatePicker({
  id,
  value,
  onChange,
  placeholder = 'Choisir une date',
  disabled = false,
  'aria-label': ariaLabel,
  className = '',
}: DatePickerProps) {
  const [open, setOpen] = useState(false)
  const selected = parseIsoDate(value)
  // Un <label> associé donne son nom au bouton et masque la date affichée : on l'annonce en
  // description.
  const valueId = useId()

  function choose(day: CalendarDay | null) {
    onChange(day ? toIsoDate(day) : null)
    setOpen(false)
  }

  return (
    <Popover.Root open={open} onOpenChange={setOpen}>
      <div className="relative">
        <Popover.Trigger
          id={id}
          disabled={disabled}
          aria-label={ariaLabel}
          aria-describedby={valueId}
          data-placeholder={selected ? undefined : ''}
          className={`${FIELD_TRIGGER_CLASS} justify-start ${selected && !disabled ? 'pr-9' : ''} ${className}`}
        >
          <CalendarDays className="h-4 w-4 shrink-0 text-text-muted" aria-hidden />
          <span id={valueId} className="min-w-0 truncate">
            {selected ? formatLongDate(selected) : placeholder}
          </span>
        </Popover.Trigger>
        {selected && !disabled && (
          <button
            type="button"
            onClick={() => onChange(null)}
            aria-label="Effacer la date"
            className="absolute inset-y-0 right-2 my-auto flex h-6 w-6 items-center justify-center rounded-md text-text-muted transition-colors hover:bg-bg hover:text-text"
          >
            <X className="h-3.5 w-3.5" aria-hidden />
          </button>
        )}
      </div>

      <Popover.Portal>
        <Popover.Content
          align="start"
          sideOffset={6}
          aria-label="Calendrier"
          // Le focus va au jour choisi (ou à aujourd'hui), pas au bouton « Mois précédent ».
          onOpenAutoFocus={(e) => e.preventDefault()}
          className="z-50 rounded-xl border border-border bg-white p-3 shadow-popover"
        >
          <Calendar selected={selected} onSelect={choose} />
          <div className="mt-3 flex items-center justify-between border-t border-border pt-2.5">
            <button type="button" onClick={() => choose(today())} className="rounded-lg px-2 py-1 text-[12.5px] font-medium text-blue-maat-text transition-colors hover:bg-blue-maat/10">
              Aujourd’hui
            </button>
            {selected && (
              <button type="button" onClick={() => choose(null)} className="rounded-lg px-2 py-1 text-[12.5px] font-medium text-text-muted transition-colors hover:bg-bg hover:text-text">
                Effacer
              </button>
            )}
          </div>
        </Popover.Content>
      </Popover.Portal>
    </Popover.Root>
  )
}
