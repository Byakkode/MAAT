import { ChevronDown, Plus, Trash2 } from 'lucide-react'
import { type ChangeEvent, type ReactNode, useId, useState } from 'react'
import type { DisclosureCompleteness, VsmeDisclosure } from '../../api/vsmeApi'
import { VSME_DISCLOSURE_LABELS } from '../../constants/vsme'
import { Card } from '../ui/Card'
import { Select } from '../ui/Select'
import { SuggestionInput } from '../ui/SuggestionInput'

// Champs de l'écran Indicateurs pour la norme volontaire (docs/specs/norme-volontaire.md,
// section 5). Libellé au-dessus du champ, unité dans le champ, deux colonnes sur grand écran :
// on lit la question et la réponse d'un seul regard. Chaque champ porte un libellé associé,
// pour la saisie au clavier et le lecteur d'écran.

export const INPUT_CLASS =
  'h-10 w-full rounded-input border border-border bg-white px-3 text-[13.5px] text-text shadow-input transition-colors ' +
  'placeholder:text-text-muted/60 focus:border-blue-maat focus:outline-none focus:ring-2 focus:ring-blue-maat/15 ' +
  '[&:read-only:not(select)]:bg-bg [&:read-only:not(select)]:shadow-none disabled:bg-bg disabled:shadow-none'

// Mention « facultatif jusqu'à 10 salariés » (§8), à côté du libellé plutôt qu'en phrase.
export function OptionalTag() {
  return (
    <span className="ml-2 rounded-full bg-border/60 px-2 py-0.5 align-middle text-[10.5px] font-medium text-text-muted">
      facultatif ≤ 10 salariés
    </span>
  )
}

// « Où trouver ? » (norme-volontaire.md, section 5) : dans quel document chercher la donnée.
// Replié par défaut, déplié sous le champ au clic, jamais au survol : lisible au clavier, au
// lecteur d'écran et sur mobile, et assez large pour trois lignes. L'aide « ce que c'est »
// reste la ligne grise sous le libellé ; celle-ci dit « où le trouver ».
function Field({
  htmlFor,
  label,
  hint,
  optional,
  wide,
  source,
  children,
}: {
  htmlFor?: string
  label: string
  hint?: string
  optional?: boolean
  wide?: boolean
  source?: string
  children: ReactNode
}) {
  const [sourceOpen, setSourceOpen] = useState(false)
  const sourceId = useId()

  return (
    <div className={wide ? 'sm:col-span-2' : undefined}>
      <div className="flex items-start justify-between gap-3">
        <label htmlFor={htmlFor} className="block text-[13px] font-medium text-text">
          {label}
          {optional && <OptionalTag />}
        </label>
        {source && (
          <button
            type="button"
            aria-expanded={sourceOpen}
            aria-controls={sourceId}
            onClick={() => setSourceOpen((open) => !open)}
            className="inline-flex shrink-0 items-center gap-0.5 text-[12px] font-medium text-blue-maat-text hover:underline"
          >
            Où trouver ?
            <ChevronDown className={`h-3.5 w-3.5 transition-transform ${sourceOpen ? 'rotate-180' : ''}`} aria-hidden />
            <span className="sr-only"> ({label})</span>
          </button>
        )}
      </div>
      {hint && <p className="mt-0.5 text-[12px] font-light text-text-muted">{hint}</p>}
      <div className="mt-1.5">{children}</div>
      {source && sourceOpen && (
        <p id={sourceId} className="mt-2 rounded-lg border border-border bg-bg px-3 py-2 text-[12.5px] leading-relaxed text-text">
          {source}
        </p>
      )}
    </div>
  )
}

export function NumberField({
  id,
  label,
  unit,
  value,
  previous,
  onChange,
  readOnly,
  hint,
  optional,
  source,
  integer = false,
}: {
  id: string
  label: string
  unit: string
  value: number | null
  previous?: number | null
  onChange: (value: number | null) => void
  readOnly: boolean
  hint?: string
  optional?: boolean
  source?: string
  integer?: boolean
}) {
  function handleChange(e: ChangeEvent<HTMLInputElement>) {
    const raw = e.target.value
    if (raw === '') {
      onChange(null)
      return
    }
    const parsed = integer ? parseInt(raw, 10) : parseFloat(raw)
    onChange(Number.isNaN(parsed) ? null : parsed)
  }

  return (
    <Field htmlFor={id} label={label} hint={hint} optional={optional} source={source}>
      <div className="relative">
        <input
          id={id}
          type="number"
          min={0}
          step={integer ? 1 : 'any'}
          value={value ?? ''}
          onChange={handleChange}
          readOnly={readOnly}
          className={`${INPUT_CLASS} pr-20 tabular-nums`}
        />
        <span className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-[12px] text-text-muted" aria-hidden>
          {unit}
        </span>
      </div>
      {previous !== undefined && previous !== null && (
        <p className="mt-1 text-[11.5px] tabular-nums text-text-muted">Exercice précédent : {previous.toLocaleString('fr-FR')} {unit}</p>
      )}
    </Field>
  )
}

// Oui / non en contrôle segmenté, avec « pas encore répondu » distinct de « non » : la
// complétude en dépend. Question à gauche, réponse à droite, sur toute la largeur.
export function YesNoField({
  name,
  label,
  value,
  onChange,
  readOnly,
  hint,
  optional,
}: {
  name: string
  label: string
  value: boolean | null
  onChange: (value: boolean) => void
  readOnly: boolean
  hint?: string
  optional?: boolean
}) {
  const labelId = `${name}-label`
  return (
    <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-2 rounded-xl border border-border bg-bg/50 px-4 py-3 sm:col-span-2">
      <div className="min-w-0 flex-1 basis-64">
        <p id={labelId} className="text-[13px] font-medium text-text">
          {label}
          {optional && <OptionalTag />}
        </p>
        {hint && <p className="mt-0.5 text-[12px] font-light text-text-muted">{hint}</p>}
      </div>
      <div role="radiogroup" aria-labelledby={labelId} className="inline-flex shrink-0 rounded-button border border-border bg-white p-0.5">
        {[true, false].map((option) => {
          const checked = value === option
          return (
            <button
              key={String(option)}
              type="button"
              role="radio"
              aria-checked={checked}
              disabled={readOnly}
              onClick={() => onChange(option)}
              className={`min-w-16 rounded-[6px] px-4 py-1.5 text-[13px] font-medium transition-colors disabled:cursor-default ${
                checked ? 'bg-blue-maat text-white shadow-button-primary' : 'text-text-muted enabled:hover:bg-bg enabled:hover:text-text'
              }`}
            >
              {option ? 'Oui' : 'Non'}
            </button>
          )
        })}
      </div>
    </div>
  )
}

export function TextField({
  id,
  label,
  value,
  onChange,
  readOnly,
  hint,
  suggestions,
  placeholder,
  wide,
}: {
  id: string
  label: string
  value: string | null
  onChange: (value: string | null) => void
  readOnly: boolean
  hint?: string
  // Saisie libre, avec des suggestions présentées dans la liste du site (SuggestionInput).
  suggestions?: string[]
  placeholder?: string
  wide?: boolean
}) {
  return (
    <Field htmlFor={id} label={label} hint={hint} wide={wide}>
      {suggestions ? (
        <SuggestionInput
          id={id}
          value={value}
          suggestions={suggestions}
          onChange={onChange}
          readOnly={readOnly}
          placeholder={placeholder}
          maxLength={200}
          className={INPUT_CLASS}
        />
      ) : (
        <input
          id={id}
          type="text"
          value={value ?? ''}
          placeholder={placeholder}
          onChange={(e) => onChange(e.target.value === '' ? null : e.target.value)}
          readOnly={readOnly}
          maxLength={200}
          className={INPUT_CLASS}
        />
      )}
    </Field>
  )
}

export function TextAreaField({
  id,
  label,
  value,
  onChange,
  readOnly,
  hint,
}: {
  id: string
  label: string
  value: string | null
  onChange: (value: string | null) => void
  readOnly: boolean
  hint?: string
}) {
  return (
    <Field htmlFor={id} label={label} hint={hint} wide>
      <textarea
        id={id}
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value === '' ? null : e.target.value)}
        readOnly={readOnly}
        maxLength={2000}
        rows={3}
        className={`${INPUT_CLASS} h-auto py-2`}
      />
    </Field>
  )
}

export function SelectField<T extends string>({
  id,
  label,
  value,
  options,
  onChange,
  readOnly,
}: {
  id: string
  label: string
  value: T | null
  options: { value: T; label: string }[]
  onChange: (value: T | null) => void
  readOnly: boolean
}) {
  return (
    <Field htmlFor={id} label={label}>
      <Select<T> id={id} value={value} options={options} onChange={onChange} disabled={readOnly} />
    </Field>
  )
}

// Choix multiples en pastilles (thèmes de B2, informations omises de B1).
export function ChipToggleGroup<T extends string>({
  label,
  hint,
  options,
  selected,
  onChange,
  readOnly,
}: {
  label: string
  hint?: string
  options: { value: T; label: string; title?: string }[]
  selected: T[]
  onChange: (selected: T[]) => void
  readOnly: boolean
}) {
  return (
    <fieldset className="sm:col-span-2">
      <legend className="text-[13px] font-medium text-text">{label}</legend>
      {hint && <p className="mt-0.5 text-[12px] font-light text-text-muted">{hint}</p>}
      <div className="mt-2 flex flex-wrap gap-2">
        {options.map((option) => {
          const pressed = selected.includes(option.value)
          return (
            <button
              key={option.value}
              type="button"
              aria-pressed={pressed}
              title={option.title}
              disabled={readOnly}
              onClick={() => onChange(pressed ? selected.filter((v) => v !== option.value) : [...selected, option.value])}
              className={`rounded-full border px-3 py-1 text-[12.5px] font-medium transition-colors disabled:cursor-default ${
                pressed
                  ? 'border-blue-maat bg-blue-maat/10 text-blue-maat-text'
                  : 'border-border bg-white text-text-muted enabled:hover:border-border-strong enabled:hover:text-text'
              }`}
            >
              {option.label}
            </button>
          )
        })}
      </div>
    </fieldset>
  )
}

const STATE_META: Record<DisclosureCompleteness['state'], { label: string; className: string }> = {
  Complete: { label: 'Complète', className: 'bg-green-maat/15 text-green-maat-text' },
  Incomplete: { label: 'À compléter', className: 'bg-orange/15 text-amber' },
  Omitted: { label: 'Omise (§22)', className: 'bg-border/60 text-text-muted' },
}

// Une information de la norme : numéro, intitulé, état de complétude tel que le calcule le
// serveur (VsmeCompleteness), et, discrètement, ce qui manque encore.
export function DisclosureCard({
  code,
  completeness,
  intro,
  children,
}: {
  code: VsmeDisclosure
  completeness: DisclosureCompleteness | undefined
  intro?: string
  children: ReactNode
}) {
  const titleId = `vsme-${code}-title`
  const state = completeness ? STATE_META[completeness.state] : null
  const missing = completeness?.state === 'Incomplete' ? completeness.missing : []

  return (
    <Card as="section" variant="flat" id={`vsme-${code}`} aria-labelledby={titleId} className="scroll-mt-24 p-6">
      <header className="flex flex-wrap items-start gap-3">
        <span className="rounded-lg bg-blue-maat/10 px-2 py-1 font-heading text-[12px] font-semibold text-blue-maat-text">{code}</span>
        <div className="min-w-0 flex-1">
          <h3 id={titleId} className="font-heading text-[15px] font-semibold leading-snug text-text">
            {VSME_DISCLOSURE_LABELS[code]}
          </h3>
          {intro && <p className="mt-1 text-[12.5px] text-text-muted">{intro}</p>}
          {missing.length > 0 && (
            <p className="mt-1 text-[12.5px] text-amber">
              <span className="font-medium">Manque :</span> {missing.join(' · ')}
            </p>
          )}
        </div>
        {state && (
          <span className={`shrink-0 rounded-full px-2.5 py-0.5 text-[11.5px] font-semibold ${state.className}`}>{state.label}</span>
        )}
      </header>
      <div className="mt-5 grid gap-x-6 gap-y-5 sm:grid-cols-2">{children}</div>
    </Card>
  )
}

// Liste d'éléments (filiales, labels, polluants, pays) : une ligne par élément. Les lignes
// laissées vides sont ignorées par le serveur.
export function ListEditor<T>({
  label,
  hint,
  items,
  onChange,
  empty,
  renderItem,
  readOnly,
  addLabel,
}: {
  label: string
  hint?: string
  items: T[]
  onChange: (items: T[]) => void
  empty: T
  renderItem: (item: T, update: (item: T) => void, index: number) => ReactNode
  readOnly: boolean
  addLabel: string
}) {
  return (
    <div className="sm:col-span-2">
      <p className="text-[13px] font-medium text-text">{label}</p>
      {hint && <p className="mt-0.5 text-[12px] font-light text-text-muted">{hint}</p>}
      {items.length > 0 && (
        <ul className="mt-2 flex flex-col gap-2">
          {items.map((item, index) => (
            <li key={index} className="flex items-center gap-2">
              <div className="grid min-w-0 flex-1 gap-2 sm:auto-cols-fr sm:grid-flow-col">
                {renderItem(item, (next) => onChange(items.map((current, i) => (i === index ? next : current))), index)}
              </div>
              {!readOnly && (
                <button
                  type="button"
                  onClick={() => onChange(items.filter((_, i) => i !== index))}
                  aria-label={`Retirer la ligne ${index + 1}`}
                  className="rounded-button p-2 text-text-muted transition-colors hover:bg-red/10 hover:text-red"
                >
                  <Trash2 className="h-4 w-4" aria-hidden />
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
      {!readOnly && (
        <button
          type="button"
          onClick={() => onChange([...items, empty])}
          className="mt-2 inline-flex items-center gap-1.5 rounded-button border border-dashed border-border-strong px-3 py-1.5 text-[12.5px] font-medium text-blue-maat-text transition-colors hover:border-blue-maat hover:bg-blue-maat/5"
        >
          <Plus className="h-3.5 w-3.5" aria-hidden />
          {addLabel}
        </button>
      )}
      {readOnly && items.length === 0 && <p className="mt-1 text-[12.5px] text-text-muted">Aucun élément.</p>}
    </div>
  )
}

export const LIST_INPUT_CLASS = INPUT_CLASS
