import type { ComponentPropsWithoutRef } from 'react'
import {
  BUTTON_BASE,
  BUTTON_SIZE_CLASSES,
  BUTTON_VARIANT_CLASSES,
} from './buttonStyles'
import { Spinner } from './Spinner'

type Variant = 'primary' | 'secondary' | 'success' | 'danger' | 'ghost'
type Size = 'sm' | 'md'

interface ButtonProps extends ComponentPropsWithoutRef<'button'> {
  variant?: Variant
  size?: Size
  isLoading?: boolean
}

export function Button({
  variant = 'primary',
  size = 'md',
  isLoading = false,
  disabled,
  className = '',
  children,
  ...props
}: ButtonProps) {
  const isDisabled = disabled || isLoading

  return (
    <button
      disabled={isDisabled}
      className={[
        BUTTON_BASE,
        'disabled:cursor-not-allowed disabled:opacity-60',
        BUTTON_VARIANT_CLASSES[variant],
        BUTTON_SIZE_CLASSES[size],
        className,
      ]
        .filter(Boolean)
        .join(' ')}
      {...props}
    >
      {isLoading && <Spinner size="sm" />}
      {children}
    </button>
  )
}
