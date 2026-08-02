import { afterEach } from 'vitest'
import { cleanup } from '@testing-library/react'
import 'vitest-axe/extend-expect'

// React Testing Library ne nettoie le DOM entre les tests que si le test runner
// expose des hooks globaux détectés automatiquement (cas de Jest) ; sans `globals: true`
// ici, l'appel explicite est nécessaire pour éviter qu'un test ne voie le rendu du
// précédent.
afterEach(() => {
  cleanup()
})

// Recharts (docs/specs/dashboard.md, section 3/4) mesure son conteneur via ResponsiveContainer
// (ResizeObserver + offsetWidth/offsetHeight), absents/nuls en jsdom par défaut : sans ce
// polyfill, le graphique se rendrait avec une taille nulle. Les tests de ce projet n'assertent
// jamais sur la géométrie SVG elle-même (voir DomainRadarChart.test.tsx) — uniquement sur la
// description accessible et le tableau alternatif — donc une taille fixe arbitraire suffit.
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
globalThis.ResizeObserver ??= ResizeObserverStub

Object.defineProperty(HTMLElement.prototype, 'offsetWidth', { configurable: true, value: 600 })
Object.defineProperty(HTMLElement.prototype, 'offsetHeight', { configurable: true, value: 300 })
