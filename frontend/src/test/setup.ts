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
