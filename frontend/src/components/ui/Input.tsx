import { forwardRef, useId } from 'react'
import type { ComponentPropsWithoutRef } from 'react'

interface InputProps extends ComponentPropsWithoutRef<'input'> {
  label: string
  error?: string
  hint?: string
}

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { label, error, hint, className = '', id: externalId, ...props },
  ref,
) {
  const generatedId = useId()
  const id = externalId ?? generatedId
  const descId = useId()
  const hasDesc = Boolean(error ?? hint)

  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-sm font-medium text-text">
        {label}
      </label>
      <input
        ref={ref}
        id={id}
        aria-describedby={hasDesc ? descId : undefined}
        aria-invalid={Boolean(error) || undefined}
        className={[
          'w-full rounded-button border px-3 py-2 text-sm text-text',
          'transition-colors duration-150',
          'placeholder:text-text-muted',
          error
            ? 'border-red focus:border-red focus:ring-red/20'
            : 'border-border focus:border-blue-maat focus:ring-blue-maat/20',
          'focus:outline-none focus:ring-2',
          'disabled:cursor-not-allowed disabled:opacity-60',
          className,
        ]
          .filter(Boolean)
          .join(' ')}
        {...props}
      />
      {hasDesc && (
        <p
          id={descId}
          role={error ? 'alert' : undefined}
          className={`text-xs ${error ? 'text-red' : 'text-text-muted'}`}
        >
          {error ?? hint}
        </p>
      )}
    </div>
  )
})
