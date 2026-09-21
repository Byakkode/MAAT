import type { ComponentPropsWithoutRef, ElementType } from 'react'

type CardVariant = 'default' | 'elevated' | 'flat'

type CardOwnProps<E extends ElementType> = {
  as?: E
  className?: string
  variant?: CardVariant
}

type CardProps<E extends ElementType> = CardOwnProps<E> &
  Omit<ComponentPropsWithoutRef<E>, keyof CardOwnProps<E>>

const VARIANT_CLASSES: Record<CardVariant, string> = {
  default: 'border border-border bg-white shadow-card transition-shadow duration-200 hover:shadow-[0_4px_16px_rgba(0,0,0,0.09)]',
  elevated: 'border border-border bg-white shadow-[0_4px_16px_rgba(0,0,0,0.09)] transition-shadow duration-200 hover:shadow-[0_8px_28px_rgba(0,0,0,0.11)]',
  flat: 'border border-border bg-white',
}

export function Card<E extends ElementType = 'div'>({
  as,
  className = '',
  variant = 'default',
  ...props
}: CardProps<E>) {
  const Tag = (as ?? 'div') as ElementType
  return (
    <Tag
      className={`rounded-card p-5 ${VARIANT_CLASSES[variant]} ${className}`}
      {...props}
    />
  )
}
