import type { ComponentPropsWithoutRef, ElementType } from 'react'

type CardOwnProps<E extends ElementType> = {
  as?: E
  className?: string
}

type CardProps<E extends ElementType> = CardOwnProps<E> &
  Omit<ComponentPropsWithoutRef<E>, keyof CardOwnProps<E>>

export function Card<E extends ElementType = 'div'>({
  as,
  className = '',
  ...props
}: CardProps<E>) {
  const Tag = (as ?? 'div') as ElementType
  return (
    <Tag
      className={`rounded-card border border-border bg-white p-5 shadow-card transition-shadow duration-200 hover:shadow-md ${className}`}
      {...props}
    />
  )
}
