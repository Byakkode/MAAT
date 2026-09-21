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
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-[13px] font-medium text-text">
        {label}
      </label>
      <input
        ref={ref}
        id={id}
        aria-describedby={hasDesc ? descId : undefined}
        aria-invalid={Boolean(error) || undefined}
        className={[
          'w-full rounded-[10px] border bg-white px-3.5 py-2.5 text-sm text-text',
          'shadow-[0_1px_2px_rgba(0,0,0,0.04)]',
          'transition-all duration-150',
          'placeholder:text-text-muted/60',
          error
            ? 'border-red/50 focus:border-red focus:ring-2 focus:ring-red/10 focus:outline-none'
            : 'border-border focus:border-blue-maat/70 focus:ring-2 focus:ring-blue-maat/10 focus:outline-none',
          'disabled:cursor-not-allowed disabled:bg-bg disabled:opacity-60',
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
