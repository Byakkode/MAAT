/* Classes Tailwind partagées entre Button.tsx et les <Link> React Router stylés en bouton.
   Import : import { buttonLinkClass } from './buttonStyles' (ou depuis '@/components/ui'). */

type Variant = 'primary' | 'secondary' | 'success' | 'danger' | 'ghost'
type Size = 'sm' | 'md'

export const BUTTON_VARIANT_CLASSES: Record<Variant, string> = {
  primary: 'bg-blue-maat text-white shadow-button hover:opacity-90 active:opacity-80',
  secondary:
    'bg-white border border-blue-maat text-blue-maat hover:bg-blue-maat/5 active:bg-blue-maat/10',
  success: 'bg-green-maat text-white shadow-button hover:opacity-90 active:opacity-80',
  danger: 'bg-red text-white shadow-button hover:opacity-90 active:opacity-80',
  ghost: 'bg-transparent text-blue-maat-text underline-offset-2 hover:underline',
}

export const BUTTON_SIZE_CLASSES: Record<Size, string> = {
  sm: 'px-3 py-1.5 text-xs',
  md: 'px-4 py-2 text-sm',
}

export const BUTTON_BASE =
  'inline-flex items-center justify-center gap-2 rounded-button font-medium transition-all duration-150'

export function buttonLinkClass(variant: Variant = 'primary', size: Size = 'md'): string {
  return [BUTTON_BASE, BUTTON_VARIANT_CLASSES[variant], BUTTON_SIZE_CLASSES[size]].join(' ')
}
