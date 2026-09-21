import type { ReactNode } from 'react'

interface PageHeaderProps {
  title: string
  subtitle?: string
  eyebrow?: string
  action?: ReactNode
}

export function PageHeader({ title, subtitle, eyebrow, action }: PageHeaderProps) {
  return (
    <div className="flex items-start justify-between gap-4">
      <div>
        {eyebrow && (
          <p className="mb-1 text-[10.5px] font-semibold uppercase tracking-widest text-blue-maat">
            {eyebrow}
          </p>
        )}
        <h1 className="text-[1.625rem] font-bold leading-tight tracking-tight text-text">{title}</h1>
        {subtitle && <p className="mt-1 text-sm text-text-muted">{subtitle}</p>}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  )
}
