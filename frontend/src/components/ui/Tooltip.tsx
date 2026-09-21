import * as RadixTooltip from '@radix-ui/react-tooltip'
import type { ReactNode } from 'react'

interface TooltipProps {
  content: ReactNode
  children: ReactNode
  side?: 'top' | 'right' | 'bottom' | 'left'
  delayDuration?: number
}

export function Tooltip({ content, children, side = 'top', delayDuration = 500 }: TooltipProps) {
  return (
    <RadixTooltip.Provider delayDuration={delayDuration}>
      <RadixTooltip.Root>
        <RadixTooltip.Trigger asChild>{children}</RadixTooltip.Trigger>
        <RadixTooltip.Portal>
          <RadixTooltip.Content
            side={side}
            sideOffset={6}
            className="z-50 rounded-lg bg-text px-2.5 py-1.5 text-xs font-medium text-white shadow-lg"
          >
            {content}
            <RadixTooltip.Arrow className="fill-text" />
          </RadixTooltip.Content>
        </RadixTooltip.Portal>
      </RadixTooltip.Root>
    </RadixTooltip.Provider>
  )
}
