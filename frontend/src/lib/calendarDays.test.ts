import { describe, expect, it } from 'vitest'
import { addMonths, formatLongDate, monthGrid, parseIsoDate, toIsoDate, weekdayIndex } from './calendarDays'

// Calendrier du site (components/ui/DatePicker.tsx).
describe('calendarDays', () => {
  it('lit et écrit le format aaaa-mm-jj sans passer par un fuseau horaire', () => {
    expect(parseIsoDate('2026-11-15')).toEqual({ year: 2026, month: 10, day: 15 })
    expect(toIsoDate({ year: 2026, month: 0, day: 5 })).toBe('2026-01-05')
    expect(parseIsoDate('')).toBeNull()
    expect(parseIsoDate('15/11/2026')).toBeNull()
  })

  it('la grille commence le lundi et couvre des semaines complètes', () => {
    // Le 1er septembre 2026 est un mardi : la grille commence le lundi 31 août.
    const grid = monthGrid(2026, 8)
    expect(grid[0]).toEqual({ year: 2026, month: 7, day: 31 })
    expect(grid.length % 7).toBe(0)
    expect(weekdayIndex(grid[0])).toBe(0)
    expect(grid.at(-1)).toEqual({ year: 2026, month: 9, day: 4 })
  })

  it('changer de mois garde le jour, ou le ramène au dernier jour du mois', () => {
    expect(addMonths({ year: 2026, month: 0, day: 31 }, 1)).toEqual({ year: 2026, month: 1, day: 28 })
    expect(addMonths({ year: 2028, month: 0, day: 31 }, 1)).toEqual({ year: 2028, month: 1, day: 29 })
    expect(addMonths({ year: 2026, month: 0, day: 15 }, -1)).toEqual({ year: 2025, month: 11, day: 15 })
  })

  it('date longue à la française', () => {
    expect(formatLongDate({ year: 2024, month: 4, day: 14 })).toBe('14 mai 2024')
    expect(formatLongDate({ year: 2024, month: 4, day: 1 })).toBe('1er mai 2024')
  })
})
