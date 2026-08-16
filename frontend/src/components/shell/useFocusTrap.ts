import { useEffect } from 'react'
import type { RefObject } from 'react'

const FOCUSABLE_SELECTOR =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

// docs/specs/coquille-et-compte.md, section 7 : "Le panneau piège le focus tant qu'il est
// ouvert et se ferme à Échap." Déplace le focus dans le conteneur à l'activation, le rend au
// clavier au démontage/fermeture, et boucle Tab/Maj+Tab sur les seuls éléments focalisables du
// conteneur tant qu'actif.
export function useFocusTrap(containerRef: RefObject<HTMLElement | null>, active: boolean, onEscape: () => void) {
  useEffect(() => {
    if (!active) {
      return
    }

    const container = containerRef.current
    if (!container) {
      return
    }

    const previouslyFocused = document.activeElement as HTMLElement | null

    function focusableElements(): HTMLElement[] {
      return Array.from(container!.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR))
    }

    focusableElements()[0]?.focus()

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault()
        onEscape()
        return
      }

      if (event.key !== 'Tab') {
        return
      }

      const items = focusableElements()
      if (items.length === 0) {
        return
      }

      const first = items[0]!
      const last = items[items.length - 1]!

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', handleKeyDown)

    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      previouslyFocused?.focus()
    }
  }, [active, containerRef, onEscape])
}
