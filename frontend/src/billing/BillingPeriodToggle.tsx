import type { BillingPeriodChoice } from './plans'

const OPTIONS: { value: BillingPeriodChoice; label: string }[] = [
  { value: 'monthly', label: 'Mensuel' },
  { value: 'yearly', label: 'Annuel (−17 %)' },
]

interface BillingPeriodToggleProps {
  value: BillingPeriodChoice
  onChange: (value: BillingPeriodChoice) => void
}

// Boutons radio natifs habillés en interrupteur segmenté : flèches du clavier, annonce « 1 sur 2 »
// et état coché fournis par le navigateur, sans ARIA à maintenir à la main.
export function BillingPeriodToggle({ value, onChange }: BillingPeriodToggleProps) {
  return (
    <fieldset className="inline-flex rounded-full border border-border bg-white p-1">
      <legend className="sr-only">Période de facturation</legend>
      {OPTIONS.map((option) => (
        <label
          key={option.value}
          className="cursor-pointer rounded-full px-4 py-1.5 font-heading text-[13px] font-medium text-text-muted transition-colors has-checked:bg-sidebar has-checked:text-white has-focus-visible:outline-2 has-focus-visible:outline-blue-maat"
        >
          <input
            type="radio"
            name="billing-period"
            value={option.value}
            checked={value === option.value}
            onChange={() => onChange(option.value)}
            className="sr-only"
          />
          {option.label}
        </label>
      ))}
    </fieldset>
  )
}
