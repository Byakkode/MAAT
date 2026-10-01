// Jours du calendrier pour le sélecteur de date (components/ui/DatePicker.tsx) : valeur
// « aaaa-mm-jj », semaine commençant le lundi, noms français. Calculs en UTC, jamais à l'heure
// locale : un jour du calendrier ne doit pas glisser d'un fuseau à l'autre (même raison que
// l'échéance d'ActionItemProgress côté serveur).

export const MONTHS = ['janvier', 'février', 'mars', 'avril', 'mai', 'juin', 'juillet', 'août', 'septembre', 'octobre', 'novembre', 'décembre']
export const WEEKDAYS_SHORT = ['lu', 'ma', 'me', 'je', 've', 'sa', 'di']
export const WEEKDAYS_LONG = ['lundi', 'mardi', 'mercredi', 'jeudi', 'vendredi', 'samedi', 'dimanche']

export interface CalendarDay {
  year: number
  month: number // 0 à 11
  day: number
}

export function parseIsoDate(value: string | null): CalendarDay | null {
  const match = value ? /^(\d{4})-(\d{2})-(\d{2})$/.exec(value) : null
  if (!match) return null
  return { year: Number(match[1]), month: Number(match[2]) - 1, day: Number(match[3]) }
}

export function toIsoDate({ year, month, day }: CalendarDay): string {
  return `${String(year).padStart(4, '0')}-${String(month + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
}

// « 14 mai 2024 »
export function formatLongDate(day: CalendarDay): string {
  return `${day.day === 1 ? '1er' : day.day} ${MONTHS[day.month]} ${day.year}`
}

export function daysInMonth(year: number, month: number): number {
  return new Date(Date.UTC(year, month + 1, 0)).getUTCDate()
}

// Lundi = 0 … dimanche = 6.
export function weekdayIndex({ year, month, day }: CalendarDay): number {
  return (new Date(Date.UTC(year, month, day)).getUTCDay() + 6) % 7
}

export function addDays(d: CalendarDay, delta: number): CalendarDay {
  const date = new Date(Date.UTC(d.year, d.month, d.day + delta))
  return { year: date.getUTCFullYear(), month: date.getUTCMonth(), day: date.getUTCDate() }
}

// Même jour le mois voisin, ramené au dernier jour du mois s'il n'existe pas (31 → 30).
export function addMonths(d: CalendarDay, delta: number): CalendarDay {
  const first = new Date(Date.UTC(d.year, d.month + delta, 1))
  const year = first.getUTCFullYear()
  const month = first.getUTCMonth()
  return { year, month, day: Math.min(d.day, daysInMonth(year, month)) }
}

export function sameDay(a: CalendarDay | null, b: CalendarDay | null): boolean {
  return !!a && !!b && a.year === b.year && a.month === b.month && a.day === b.day
}

export function today(): CalendarDay {
  const now = new Date()
  return { year: now.getFullYear(), month: now.getMonth(), day: now.getDate() }
}

// Les jours affichés : semaines complètes, du lundi précédant le 1er au dimanche suivant le
// dernier jour du mois.
export function monthGrid(year: number, month: number): CalendarDay[] {
  const first: CalendarDay = { year, month, day: 1 }
  const start = addDays(first, -weekdayIndex(first))
  const cells = Math.ceil((weekdayIndex(first) + daysInMonth(year, month)) / 7) * 7
  return Array.from({ length: cells }, (_, i) => addDays(start, i))
}

