import type { ChangeEvent, ReactNode } from 'react'
import type { DisclosureCompleteness, VsmeDisclosure } from '../../api/vsmeApi'
import { VSME_DISCLOSURE_LABELS } from '../../constants/vsme'
import { Badge } from '../ui/Badge'
import { Card } from '../ui/Card'

// Champs de l'écran Indicateurs pour la norme volontaire (docs/specs/norme-volontaire.md,
// section 5). Chaque champ porte un libellé associé : l'écran se remplit au clavier et se lit
// au lecteur d'écran.

const INPUT_CLASS =
  'rounded-lg border border-border bg-white px-2.5 py-1.5 text-[13px] text-text transition-colors focus:border-blue-maat focus:outline-none focus:ring-1 focus:ring-blue-maat/20 read-only:bg-bg disabled:bg-bg'

function Row({ htmlFor, label, hint, children }: { htmlFor?: string; label: string; hint?: string; children: ReactNode }) {
  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5 border-b border-border py-2.5 last:border-0">
      <div className="min-w-0 flex-1 basis-56">
        <label htmlFor={htmlFor} className="text-[13px] text-text-muted">
          {label}
        </label>
        {hint && <p className="text-[11.5px] font-light text-text-muted">{hint}</p>}
      </div>
      {children}
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
    <Row htmlFor={id} label={label} hint={hint}>
      <div className="flex items-center gap-2">
        {previous !== undefined && previous !== null && (
          <span className="text-[11px] tabular-nums text-text-muted">N-1 : {previous.toLocaleString('fr-FR')}</span>
        )}
        <input
          id={id}
          type="number"
          min={0}
          step={integer ? 1 : 'any'}
          value={value ?? ''}
          onChange={handleChange}
          readOnly={readOnly}
          className={`${INPUT_CLASS} w-32 text-right tabular-nums`}
        />
        <span className="w-20 shrink-0 text-[11.5px] text-text-muted">{unit}</span>
      </div>
    </Row>
  )
}

// Oui / non, avec « pas encore répondu » distinct de « non » : la complétude en dépend.
export function YesNoField({
  name,
  label,
  value,
  onChange,
  readOnly,
  hint,
}: {
  name: string
  label: string
  value: boolean | null
  onChange: (value: boolean) => void
  readOnly: boolean
  hint?: string
}) {
  return (
    <fieldset className="flex flex-wrap items-center gap-x-3 gap-y-1.5 border-b border-border py-2.5 last:border-0">
      <legend className="sr-only">{label}</legend>
      <div className="min-w-0 flex-1 basis-56" aria-hidden>
        <p className="text-[13px] text-text-muted">{label}</p>
        {hint && <p className="text-[11.5px] font-light text-text-muted">{hint}</p>}
      </div>
      <div className="flex gap-4">
        {[true, false].map((option) => (
          <label key={String(option)} className="flex items-center gap-1.5 text-[13px] text-text">
            <input
              type="radio"
              name={name}
              checked={value === option}
              onChange={() => onChange(option)}
              disabled={readOnly}
              className="accent-blue-maat"
            />
            {option ? 'Oui' : 'Non'}
          </label>
        ))}
      </div>
    </fieldset>
  )
}

export function TextField({
  id,
  label,
  value,
  onChange,
  readOnly,
  hint,
  list,
  placeholder,
}: {
  id: string
  label: string
  value: string | null
  onChange: (value: string | null) => void
  readOnly: boolean
  hint?: string
  list?: string
  placeholder?: string
}) {
  return (
    <Row htmlFor={id} label={label} hint={hint}>
      <input
        id={id}
        type="text"
        value={value ?? ''}
        list={list}
        placeholder={placeholder}
        onChange={(e) => onChange(e.target.value === '' ? null : e.target.value)}
        readOnly={readOnly}
        maxLength={200}
        className={`${INPUT_CLASS} w-full sm:w-64`}
      />
    </Row>
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
    <div className="border-b border-border py-2.5 last:border-0">
      <label htmlFor={id} className="text-[13px] text-text-muted">
        {label}
      </label>
      {hint && <p className="text-[11.5px] font-light text-text-muted">{hint}</p>}
      <textarea
        id={id}
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value === '' ? null : e.target.value)}
        readOnly={readOnly}
        maxLength={2000}
        rows={3}
        className={`${INPUT_CLASS} mt-1.5 w-full`}
      />
    </div>
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
    <Row htmlFor={id} label={label}>
      <select
        id={id}
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value === '' ? null : (e.target.value as T))}
        disabled={readOnly}
        className={`${INPUT_CLASS} w-full sm:w-64`}
      >
        <option value="">Choisir…</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </Row>
  )
}

const STATE_BADGE: Record<DisclosureCompleteness['state'], { label: string; variant: 'green' | 'amber' | 'default' }> = {
  Complete: { label: 'Complète', variant: 'green' },
  Incomplete: { label: 'À compléter', variant: 'amber' },
  Omitted: { label: 'Omise (§22)', variant: 'default' },
}

// Une information de la norme : son numéro, son intitulé, son état de complétude tel que le
// calcule le serveur (VsmeCompleteness), et ce qui manque encore.
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
  const state = completeness ? STATE_BADGE[completeness.state] : null

  return (
    <Card as="section" id={`vsme-${code}`} aria-labelledby={titleId} className="scroll-mt-6">
      <div className="mb-2 flex flex-wrap items-center gap-2">
        <span className="rounded-md bg-blue-maat/10 px-1.5 py-0.5 text-[12px] font-semibold text-blue-maat-text">{code}</span>
        <h3 id={titleId} className="min-w-0 flex-1 text-[14px] font-semibold text-text">
          {VSME_DISCLOSURE_LABELS[code]}
        </h3>
        {state && <Badge variant={state.variant}>{state.label}</Badge>}
      </div>
      {intro && <p className="mb-2 text-[12.5px] text-text-muted">{intro}</p>}
      {completeness?.state === 'Incomplete' && completeness.missing.length > 0 && (
        <p className="mb-2 rounded-lg bg-kpi-amber px-3 py-2 text-[12.5px] text-amber">
          À compléter : {completeness.missing.join(', ')}
        </p>
      )}
      <div>{children}</div>
    </Card>
  )
}

// Liste d'éléments (filiales, labels, polluants, pays) : une ligne par élément, un bouton pour
// en ajouter, un pour retirer. Les lignes laissées vides sont ignorées par le serveur.
export function ListEditor<T>({
  label,
  items,
  onChange,
  empty,
  renderItem,
  readOnly,
  addLabel,
}: {
  label: string
  items: T[]
  onChange: (items: T[]) => void
  empty: T
  renderItem: (item: T, update: (item: T) => void, index: number) => ReactNode
  readOnly: boolean
  addLabel: string
}) {
  return (
    <div className="border-b border-border py-2.5 last:border-0">
      <p className="text-[13px] text-text-muted">{label}</p>
      <ul className="mt-1.5 flex flex-col gap-2">
        {items.map((item, index) => (
          <li key={index} className="flex flex-wrap items-center gap-2">
            {renderItem(item, (next) => onChange(items.map((current, i) => (i === index ? next : current))), index)}
            {!readOnly && (
              <button
                type="button"
                onClick={() => onChange(items.filter((_, i) => i !== index))}
                className="text-[12.5px] font-medium text-red hover:underline"
              >
                Retirer
              </button>
            )}
          </li>
        ))}
      </ul>
      {!readOnly && (
        <button
          type="button"
          onClick={() => onChange([...items, empty])}
          className="mt-2 text-[12.5px] font-medium text-blue-maat-text hover:underline"
        >
          {addLabel}
        </button>
      )}
    </div>
  )
}

export const LIST_INPUT_CLASS = `${INPUT_CLASS} min-w-0 flex-1 basis-40`
