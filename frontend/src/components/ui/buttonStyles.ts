/* Classes Tailwind partagées entre Button.tsx et les <Link> React Router stylés en bouton.
   Import : import { buttonLinkClass } from './buttonStyles' (ou depuis '@/components/ui'). */

type Variant = 'primary' | 'secondary' | 'success' | 'danger' | 'ghost'
type Size = 'sm' | 'md' | 'lg'

export const BUTTON_VARIANT_CLASSES: Record<Variant, string> = {
  primary:
    'bg-blue-maat text-white shadow-button ' +
    'hover:bg-blue-maat-text hover:shadow-[0_4px_14px_rgba(21,101,255,0.28)] ' +
    'active:scale-[0.97] active:shadow-none',
  secondary:
    'bg-white border border-border text-text shadow-button ' +
    'hover:bg-bg hover:border-border-strong ' +
    'active:scale-[0.97]',
  success:
    'bg-green-maat text-white shadow-button ' +
    'hover:opacity-90 hover:shadow-[0_4px_14px_rgba(41,204,106,0.25)] ' +
    'active:scale-[0.97] active:shadow-none',
  danger:
    'bg-red text-white shadow-button ' +
    'hover:opacity-90 hover:shadow-[0_4px_14px_rgba(229,57,53,0.25)] ' +
    'active:scale-[0.97] active:shadow-none',
  ghost:
    'bg-transparent text-text-muted ' +
    'hover:bg-bg hover:text-text ' +
    'active:scale-[0.97]',
}

export const BUTTON_SIZE_CLASSES: Record<Size, string> = {
  sm: 'px-3 py-1.5 text-xs',
  md: 'px-4 py-2 text-sm',
  lg: 'px-5 py-2.5 text-sm',
}

export const BUTTON_BASE =
  'inline-flex items-center justify-center gap-2 rounded-button font-medium ' +
  'transition-all duration-150 cursor-pointer'

export function buttonLinkClass(variant: Variant = 'primary', size: Size = 'md'): string {
  return [BUTTON_BASE, BUTTON_VARIANT_CLASSES[variant], BUTTON_SIZE_CLASSES[size]].join(' ')
}
