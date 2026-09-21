type Size = 'sm' | 'md' | 'lg'

const SIZE_CLASSES: Record<Size, string> = {
  sm: 'w-3.5 h-3.5 border-[1.5px]',
  md: 'w-5 h-5 border-2',
  lg: 'w-7 h-7 border-2',
}

export function Spinner({ size = 'md', label = 'Chargement…' }: { size?: Size; label?: string }) {
  return (
    <span
      role="status"
      aria-label={label}
      className={[
        'inline-block animate-spin rounded-full',
        'border-border border-t-blue-maat',
        SIZE_CLASSES[size],
      ].join(' ')}
    />
  )
}
