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
          <p className="mb-1.5 text-[11px] font-medium tracking-wide text-blue-maat/80">
            {eyebrow}
          </p>
        )}
        <h1 className="text-[1.5rem] font-semibold leading-tight tracking-tight text-text">{title}</h1>
        {subtitle && <p className="mt-1 text-[13px] text-text-muted">{subtitle}</p>}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  )
}
